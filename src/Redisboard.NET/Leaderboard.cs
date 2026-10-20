using System.Globalization;
using Redisboard.NET.Enumerations;
using Redisboard.NET.Helpers;
using Redisboard.NET.Interfaces;
using ILeaderboardSerializer = Redisboard.NET.Serialization.ILeaderboardSerializer;
using StackExchange.Redis;

namespace Redisboard.NET;

/// <summary>
/// Represents Redis-backed leaderboard operations for entities of type <typeparamref name="TEntity"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Leaderboard{TEntity}"/> stores ranking data in Redis sorted sets and entity metadata in a
/// Redis hash so callers can read and update strongly typed entities without issuing raw Redis commands.
/// </para>
/// <para>
/// Scores are written to Redis as their negated values so the native ascending sorted set order yields a
/// descending leaderboard. Read operations normalize those scores before returning entities and populate
/// <see cref="ILeaderboardEntity.Rank"/> according to the requested <see cref="RankingType"/>.
/// </para>
/// </remarks>
/// <typeparam name="TEntity">
/// Type stored in the leaderboard. It must implement <see cref="ILeaderboardEntity"/>, expose a
/// parameterless constructor, and declare exactly one key property and one score property by using
/// <see cref="Attributes.LeaderboardKeyAttribute"/> and <see cref="Attributes.LeaderboardScoreAttribute"/>.
/// </typeparam>
public class Leaderboard<TEntity> : ILeaderboard<TEntity>
    where TEntity : ILeaderboardEntity, new()
{
    /// <summary>
    /// Default maximum number of entities or entity keys allowed in one batch operation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This guardrail limits batch-oriented Lua script payloads so one call does not send an excessively large
    /// argument list to Redis.
    /// </para>
    /// <para>
    /// <see cref="AddEntitiesAsync(StackExchange.Redis.RedisValue,System.Collections.Generic.IEnumerable{TEntity},bool,System.Threading.CancellationToken)"/>
    /// and <see cref="DeleteEntitiesAsync(StackExchange.Redis.RedisValue,System.Collections.Generic.IEnumerable{StackExchange.Redis.RedisValue},System.Threading.CancellationToken)"/>
    /// enforce this default limit and throw <see cref="ArgumentOutOfRangeException"/> when callers exceed it.
    /// </para>
    /// </remarks>
    public const int DefaultMaxBatchOperationSize = 10_000;

    private readonly IDatabase _redis;
    private readonly ILeaderboardSerializer _serializer;
    private readonly LeaderboardOptions _options;

    /// <summary>
    /// Initializes a new instance that uses an existing Redis database and serializer.
    /// </summary>
    /// <param name="redis">Redis database used for all leaderboard commands.</param>
    /// <param name="serializer">Serializes and deserializes entity metadata stored alongside ranking data.</param>
    /// <remarks>
    /// Use this overload when application code already controls creation of <see cref="IDatabase"/> and needs
    /// <see cref="Leaderboard{TEntity}"/> to reuse that database selection and connection lifetime.
    /// </remarks>
    /// <exception cref="Exceptions.LeaderboardConfigurationException">Thrown when the entity key or score configuration is invalid.</exception>
    public Leaderboard(IDatabase redis, ILeaderboardSerializer serializer)
        : this(redis, serializer, new LeaderboardOptions())
    {
    }

    /// <summary>Initializes a leaderboard with explicit Redis key naming options.</summary>
    /// <param name="redis">Redis database used for all leaderboard commands.</param>
    /// <param name="serializer">Serializes entity metadata.</param>
    /// <param name="options">Key naming settings copied for this instance.</param>
    /// <remarks>Changing settings selects a different namespace without migrating data. See <see cref="LeaderboardOptions"/>.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when options or its key prefix is null.</exception>
    /// <exception cref="ArgumentException">Thrown when cluster mode uses a prefix containing braces.</exception>
    /// <exception cref="Exceptions.LeaderboardConfigurationException">Thrown when entity configuration is invalid.</exception>
    public Leaderboard(IDatabase redis, ILeaderboardSerializer serializer, LeaderboardOptions options)
    {
        if (options is null)
            throw new ArgumentNullException(nameof(options));
        this._options = options.Snapshot();
        EntityTypeAccessor<TEntity>.Validate();
        this._redis = redis;
        this._serializer = serializer;
    }

    /// <summary>
    /// Initializes a new instance that resolves its Redis database from an <see cref="IConnectionMultiplexer"/>.
    /// </summary>
    /// <param name="connectionMultiplexer">Redis multiplexer used to resolve the target database.</param>
    /// <param name="serializer">Serializes and deserializes entity metadata stored alongside ranking data.</param>
    /// <param name="databaseIndex">Zero-based Redis database index, or -1 to use the configured default, passed to <see cref="IConnectionMultiplexer.GetDatabase(int, object)"/>.</param>
    /// <remarks>
    /// Use this overload when dependency injection or application startup manages the shared
    /// <see cref="IConnectionMultiplexer"/> and leaderboard instances should select their own database index.
    /// </remarks>
    /// <exception cref="Exceptions.LeaderboardConfigurationException">Thrown when the entity key or score configuration is invalid.</exception>
    public Leaderboard(
        IConnectionMultiplexer connectionMultiplexer,
        ILeaderboardSerializer serializer,
        int databaseIndex = -1)
        : this(connectionMultiplexer.GetDatabase(databaseIndex), serializer)
    {
    }

    /// <summary>Initializes a leaderboard with explicit Redis key naming and database selection.</summary>
    /// <param name="connectionMultiplexer">Shared Redis connection used to select the database.</param>
    /// <param name="serializer">Serializes entity metadata.</param>
    /// <param name="options">Key naming settings copied for this instance.</param>
    /// <param name="databaseIndex">Database index, or -1 for the configured default. Redis Cluster supports database zero only.</param>
    /// <remarks>The caller retains connection ownership. See <see cref="LeaderboardOptions"/> for namespace compatibility.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when options or its key prefix is null.</exception>
    /// <exception cref="ArgumentException">Thrown when cluster mode uses a prefix containing braces.</exception>
    /// <exception cref="Exceptions.LeaderboardConfigurationException">Thrown when entity configuration is invalid.</exception>
    public Leaderboard(IConnectionMultiplexer connectionMultiplexer, ILeaderboardSerializer serializer,
        LeaderboardOptions options, int databaseIndex = -1)
        : this(connectionMultiplexer.GetDatabase(databaseIndex), serializer, options)
    {
    }

    /// <summary>
    /// Adds an entity to the specified leaderboard.
    /// </summary>
    /// <param name="leaderboardKey">Identifies leaderboard whose ranking and metadata structures should receive <paramref name="entity"/>.</param>
    /// <param name="entity">Entity to add. Its configured key and score properties supply the Redis member identifier and ranking score. The score must be finite and non-negative.</param>
    /// <param name="fireAndForget">When <see langword="true"/>, sends the write without waiting for Redis to acknowledge completion.</param>
    /// <param name="cancellationToken">Cancels operation before Redis script execution begins.</param>
    /// <remarks>
    /// <para>
    /// This method writes ranking data to the leaderboard sorted set, tracks distinct scores in the auxiliary
    /// score set, and stores serialized entity metadata in the leaderboard hash.
    /// </para>
    /// <para>
    /// If the entity key already exists, its score and metadata are replaced. A previous score remains in
    /// the auxiliary set only while at least one entity still holds it, preserving <see cref="RankingType.DenseRank"/>.
    /// </para>
    /// <para>
    /// Entity keys and scores are validated before serialization. Scores are negated before persistence so the
    /// highest score appears first when Redis returns ascending sorted set results.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="leaderboardKey"/> or entity key is missing.</exception>
    /// <exception cref="ArgumentException">Thrown when entity key is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when entity key is non-positive or entity score is negative or nonfinite.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled before script execution.</exception>
    public async Task AddEntityAsync(
        RedisValue leaderboardKey,
        TEntity entity,
        bool fireAndForget = false,
        CancellationToken cancellationToken = default)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);

        var entityKey = EntityTypeAccessor<TEntity>.GetKey(entity);
        Guard.AgainstInvalidIdentityKey(entityKey);

        var score = EntityTypeAccessor<TEntity>.GetScore(entity);
        Guard.AgainstInvalidScore(score);

        var metadata = this._serializer.Serialize(entity);

        var invertedScore = -score;

        RedisKey[] keys =
        [
            CacheKey.ForLeaderboardSortedSet(leaderboardKey, this._options),
            CacheKey.ForUniqueScoreSortedSet(leaderboardKey, this._options),
            CacheKey.ForEntityDataHashSet(leaderboardKey, this._options)
        ];

        RedisValue[] args = [entityKey, invertedScore, metadata];

        var script = LeaderboardScript.ForAddEntitiesBatch().ExecutableScript;
        var commandFlags = fireAndForget ? CommandFlags.FireAndForget : CommandFlags.None;

        cancellationToken.ThrowIfCancellationRequested();
        await this._redis.ScriptEvaluateAsync(script, keys, args, commandFlags);
    }

    /// <summary>
    /// Adds multiple entities to the specified leaderboard in one batch operation.
    /// </summary>
    /// <param name="leaderboardKey">Identifies leaderboard whose ranking and metadata structures should receive <paramref name="entities"/>.</param>
    /// <param name="entities">Entities to add. Each entry must provide a valid key and finite, non-negative score.</param>
    /// <param name="fireAndForget">When <see langword="true"/>, sends the write without waiting for Redis to acknowledge completion.</param>
    /// <param name="cancellationToken">Cancels operation before Redis script execution begins.</param>
    /// <remarks>
    /// <para>
    /// Batch writes reduce Redis round trips by sending all entity triplets through one Lua script execution.
    /// </para>
    /// <para>
    /// Existing entity keys have their scores and metadata replaced. Entries are applied in enumeration order;
    /// the last entry for a repeated key supplies its final score and metadata. Score groups abandoned by
    /// replaced entries are removed so <see cref="RankingType.DenseRank"/> remains correct.
    /// </para>
    /// <para>
    /// The batch must contain at least one entity and no more than <see cref="DefaultMaxBatchOperationSize"/> items.
    /// Each entity is validated and serialized before the script runs.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="leaderboardKey"/> is missing or <paramref name="entities"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="entities"/> is empty or contains an entity with an empty key.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when batch size exceeds <see cref="DefaultMaxBatchOperationSize"/>, an entity key is non-positive, or an entity score is negative or nonfinite.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled before script execution.</exception>
    public async Task AddEntitiesAsync(
        RedisValue leaderboardKey,
        IEnumerable<TEntity> entities,
        bool fireAndForget = false,
        CancellationToken cancellationToken = default)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);
        Guard.AgainstNullOrEmptyCollection(entities, nameof(entities));

        var entries = entities.ToArray();

        Guard.AgainstCollectionSizeExceeded(entries.Length, DefaultMaxBatchOperationSize, nameof(entities));

        var args = new RedisValue[entries.Length * 3];

        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];

            var entityKey = EntityTypeAccessor<TEntity>.GetKey(entry);
            Guard.AgainstInvalidIdentityKey(entityKey);

            var score = EntityTypeAccessor<TEntity>.GetScore(entry);
            Guard.AgainstInvalidScore(score);

            var metadata = this._serializer.Serialize(entry);
            Guard.AgainstInvalidMetadata(metadata);

            var invertedScore = -score;
            var index = i * 3;

            args[index] = entityKey;
            args[index + 1] = invertedScore;
            args[index + 2] = metadata;
        }

        RedisKey[] keys =
        [
            CacheKey.ForLeaderboardSortedSet(leaderboardKey, this._options),
            CacheKey.ForUniqueScoreSortedSet(leaderboardKey, this._options),
            CacheKey.ForEntityDataHashSet(leaderboardKey, this._options)
        ];

        var script = LeaderboardScript.ForAddEntitiesBatch().ExecutableScript;
        var commandFlags = fireAndForget ? CommandFlags.FireAndForget : CommandFlags.None;

        cancellationToken.ThrowIfCancellationRequested();
        await this._redis.ScriptEvaluateAsync(script, keys, args, commandFlags);
    }

    /// <summary>
    /// Updates the score of an existing entity without rewriting its metadata.
    /// </summary>
    /// <param name="leaderboardKey">Identifies leaderboard that contains <paramref name="entity"/>.</param>
    /// <param name="entity">Entity whose configured key identifies the stored member and whose score property contains the finite, non-negative replacement score.</param>
    /// <param name="fireAndForget">When <see langword="true"/>, sends the write without waiting for Redis to acknowledge completion.</param>
    /// <param name="cancellationToken">Cancels operation before Redis script execution begins.</param>
    /// <remarks>
    /// Missing entities or records without both ranking and metadata are left unchanged atomically.
    /// This method updates ranking state only. Stored metadata remains unchanged until
    /// <see cref="UpdateEntityMetadataAsync(StackExchange.Redis.RedisValue,TEntity,bool,System.Threading.CancellationToken)"/> runs.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="leaderboardKey"/> or entity key is missing.</exception>
    /// <exception cref="ArgumentException">Thrown when entity key is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when entity key is non-positive or replacement score is negative or nonfinite.</exception>
    public Task UpdateEntityScoreAsync(
        RedisValue leaderboardKey,
        TEntity entity,
        bool fireAndForget = false,
        CancellationToken cancellationToken = default)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);

        var entityKey = EntityTypeAccessor<TEntity>.GetKey(entity);
        Guard.AgainstInvalidIdentityKey(entityKey);

        var newScore = EntityTypeAccessor<TEntity>.GetScore(entity);
        Guard.AgainstInvalidScore(newScore);

        return this.InternalUpdateEntityScoreAsync(leaderboardKey, entityKey, newScore, fireAndForget, cancellationToken);
    }

    /// <summary>
    /// Updates the score of an existing entity without rewriting its metadata.
    /// </summary>
    /// <param name="leaderboardKey">Identifies leaderboard that contains the entity.</param>
    /// <param name="entityKey">Identifies the entity whose score should be updated.</param>
    /// <param name="score">Replacement score. Must be finite and non-negative.</param>
    /// <param name="fireAndForget">When <see langword="true"/>, sends the write without waiting for Redis to acknowledge completion.</param>
    /// <param name="cancellationToken">Cancels operation before Redis script execution begins.</param>
    /// <remarks>
    /// Missing entities or records without both ranking and metadata are left unchanged atomically.
    /// This method updates ranking state only. Stored metadata remains unchanged until
    /// <see cref="UpdateEntityMetadataAsync(StackExchange.Redis.RedisValue,TEntity,bool,System.Threading.CancellationToken)"/> runs.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="leaderboardKey"/> or <paramref name="entityKey"/> is missing.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="entityKey"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="entityKey"/> is non-positive or <paramref name="score"/> is negative or nonfinite.</exception>
    public Task UpdateEntityScoreAsync(
        RedisValue leaderboardKey,
        RedisValue entityKey,
        double score,
        bool fireAndForget = false,
        CancellationToken cancellationToken = default)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);
        Guard.AgainstInvalidIdentityKey(entityKey);
        Guard.AgainstInvalidScore(score);

        return this.InternalUpdateEntityScoreAsync(leaderboardKey, entityKey, score, fireAndForget, cancellationToken);
    }

    /// <summary>
    /// Replaces the stored metadata for an existing entity without changing its score.
    /// </summary>
    /// <param name="leaderboardKey">Identifies leaderboard that contains <paramref name="entity"/>.</param>
    /// <param name="entity">Entity whose configured key identifies the stored member and whose serialized state becomes the new metadata payload.</param>
    /// <param name="fireAndForget">When <see langword="true"/>, sends the write without waiting for Redis to acknowledge completion.</param>
    /// <param name="cancellationToken">Cancels operation before Redis script execution begins.</param>
    /// <remarks>
    /// Missing entities or records without both ranking and metadata are left unchanged atomically.
    /// Ranking order does not change. Call
    /// <see cref="UpdateEntityScoreAsync(StackExchange.Redis.RedisValue,TEntity,bool,System.Threading.CancellationToken)"/> when score changes must also be persisted.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="leaderboardKey"/> or entity key is missing.</exception>
    /// <exception cref="ArgumentException">Thrown when entity key is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when entity key is non-positive.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled before script execution.</exception>
    public async Task UpdateEntityMetadataAsync(
        RedisValue leaderboardKey,
        TEntity entity,
        bool fireAndForget = false,
        CancellationToken cancellationToken = default)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);

        var entityKey = EntityTypeAccessor<TEntity>.GetKey(entity);
        Guard.AgainstInvalidIdentityKey(entityKey);

        var metadata = this._serializer.Serialize(entity);

        var commandFlags = fireAndForget ? CommandFlags.FireAndForget : CommandFlags.None;

        RedisKey[] keys =
        [
            CacheKey.ForLeaderboardSortedSet(leaderboardKey, this._options),
            CacheKey.ForEntityDataHashSet(leaderboardKey, this._options)
        ];
        RedisValue[] args = [entityKey, metadata];

        cancellationToken.ThrowIfCancellationRequested();
        await this._redis.ScriptEvaluateAsync(
            LeaderboardScript.ForUpdateEntityMetadata().ExecutableScript, keys, args, commandFlags);
    }

    /// <summary>
    /// Returns an entity together with neighboring entities around its current rank.
    /// </summary>
    /// <param name="leaderboardKey">Identifies leaderboard to query.</param>
    /// <param name="entityKey">Identifies entity whose neighborhood should be returned.</param>
    /// <param name="offset">Maximum number of entities to include above and below target entity. Cannot be negative.</param>
    /// <param name="rankingType">Ranking algorithm used to populate <see cref="ILeaderboardEntity.Rank"/> on returned entities.</param>
    /// <param name="cancellationToken">Cancels operation before or during downstream leaderboard reads.</param>
    /// <returns>Array containing target entity and nearby entities ordered by rank, or an empty array when target entity does not exist.</returns>
    /// <remarks>
    /// A read-only Lua script resolves the target position, neighboring scores, ranks, and metadata atomically.
    /// The window is clamped at the top of the leaderboard.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="leaderboardKey"/> or <paramref name="entityKey"/> is missing.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="entityKey"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="entityKey"/> is non-positive or <paramref name="offset"/> is negative.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when <paramref name="rankingType"/> is not supported.</exception>
    public async Task<TEntity[]> GetEntityAndNeighboursAsync(
        RedisValue leaderboardKey,
        RedisValue entityKey,
        int offset = 10,
        RankingType rankingType = RankingType.Default,
        CancellationToken cancellationToken = default)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);
        Guard.AgainstInvalidIdentityKey(entityKey);
        Guard.AgainstInvalidOffset(offset);

        return await this.ReadEntitiesAsync(leaderboardKey, "neighbors", entityKey, offset, rankingType, cancellationToken);
    }

    /// <summary>
    /// Returns entities whose scores fall within an inclusive score range.
    /// </summary>
    /// <param name="leaderboardKey">Identifies leaderboard to query.</param>
    /// <param name="minScore">Inclusive lower bound for score filter. Cannot be negative.</param>
    /// <param name="maxScore">Inclusive upper bound for score filter. Cannot be negative and must be greater than or equal to <paramref name="minScore"/>.</param>
    /// <param name="rankingType">Ranking algorithm used to populate <see cref="ILeaderboardEntity.Rank"/> on returned entities.</param>
    /// <param name="cancellationToken">Cancels operation before or during downstream leaderboard reads.</param>
    /// <returns>Entities that match requested score range, ordered by rank, or an empty array when no entries match.</returns>
    /// <remarks>
    /// A read-only Lua script selects matching scores and returns ranks and metadata in one snapshot.
    /// Matching keys cross the network only with their returned entities. Wide ranges return every matching
    /// entity and therefore increase Redis script time, response size, and client allocations.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="leaderboardKey"/> is missing.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="minScore"/> or <paramref name="maxScore"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="minScore"/> is greater than <paramref name="maxScore"/>.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when <paramref name="rankingType"/> is not supported.</exception>
    public async Task<TEntity[]> GetEntitiesByScoreRangeAsync(
        RedisValue leaderboardKey,
        double minScore,
        double maxScore,
        RankingType rankingType = RankingType.Default,
        CancellationToken cancellationToken = default)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);
        Guard.AgainstInvalidScoreRangeLimit(minScore);
        Guard.AgainstInvalidScoreRangeLimit(maxScore);
        Guard.AgainstInvalidScoreRange(minScore, maxScore);

        RedisValue lowerBound = double.IsPositiveInfinity(maxScore) ? "-inf" : (RedisValue)(-maxScore);
        RedisValue upperBound = double.IsPositiveInfinity(minScore) ? "-inf" : (RedisValue)(-minScore);
        return await this.ReadEntitiesAsync(leaderboardKey, "score", lowerBound, upperBound, rankingType, cancellationToken);
    }

    /// <summary>
    /// Returns entities whose ranks fall within an inclusive rank range.
    /// </summary>
    /// <param name="leaderboardKey">Identifies leaderboard to query.</param>
    /// <param name="startRank">One-based starting rank for page.</param>
    /// <param name="endRank">One-based ending rank for page. Must be greater than or equal to <paramref name="startRank"/>.</param>
    /// <param name="rankingType">Ranking algorithm used to populate <see cref="ILeaderboardEntity.Rank"/> on returned entities.</param>
    /// <param name="cancellationToken">Cancels operation before or during leaderboard reads.</param>
    /// <returns>Entities within requested rank range ordered by rank.</returns>
    /// <remarks>
    /// Rank values are one-based at API boundary even though Redis sorted set indexes are zero-based internally.
    /// A read-only Lua script reads scores, ranks, and metadata in one snapshot, preventing concurrent writes
    /// from mixing entity data from different leaderboard states.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="leaderboardKey"/> is missing.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="startRank"/> is less than 1 or <paramref name="endRank"/> is less than <paramref name="startRank"/>.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when <paramref name="rankingType"/> is not supported.</exception>
    public async Task<TEntity[]> GetEntitiesByRankRangeAsync(
        RedisValue leaderboardKey,
        long startRank,
        long endRank,
        RankingType rankingType = RankingType.Default,
        CancellationToken cancellationToken = default)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);
        Guard.AgainstInvalidRankRange(startRank, endRank);

        cancellationToken.ThrowIfCancellationRequested();

        var startIndex = startRank - 1;
        var endIndex = endRank - 1;

        return await this.ReadEntitiesAsync(leaderboardKey, "rank", startIndex, endIndex, rankingType, cancellationToken);
    }

    /// <summary>
    /// Returns current score for a single entity.
    /// </summary>
    /// <param name="leaderboardKey">Identifies leaderboard to query.</param>
    /// <param name="entityKey">Identifies entity whose score should be returned.</param>
    /// <param name="cancellationToken">Reserved for API symmetry. This implementation does not observe it after validation.</param>
    /// <returns>Normalized score stored for entity, or <see langword="null"/> when entity does not exist.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="leaderboardKey"/> or <paramref name="entityKey"/> is missing.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="entityKey"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="entityKey"/> is non-positive.</exception>
    public async Task<double?> GetEntityScoreAsync(
        RedisValue leaderboardKey,
        RedisValue entityKey,
        CancellationToken cancellationToken = default)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);
        Guard.AgainstInvalidIdentityKey(entityKey);

        var score = await this._redis.SortedSetScoreAsync(
            CacheKey.ForLeaderboardSortedSet(leaderboardKey, this._options), entityKey);

        return score.HasValue ? NormalizeScore(score.Value) : null;
    }

    /// <summary>
    /// Returns current rank for a single entity.
    /// </summary>
    /// <param name="leaderboardKey">Identifies leaderboard to query.</param>
    /// <param name="entityKey">Identifies entity whose rank should be returned.</param>
    /// <param name="rankingType">Ranking algorithm used to compute returned rank.</param>
    /// <param name="cancellationToken">Cancels operation before or during downstream leaderboard reads.</param>
    /// <returns>One-based rank for entity, or <see langword="null"/> when entity does not exist.</returns>
    /// <remarks>
    /// Default ranking resolves directly from Redis rank indexes. Other ranking modes calculate the rank in one
    /// read-only Lua script without fetching or deserializing entity metadata.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="leaderboardKey"/> or <paramref name="entityKey"/> is missing.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="entityKey"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="entityKey"/> is non-positive.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when <paramref name="rankingType"/> is not supported.</exception>
    public async Task<long?> GetEntityRankAsync(
        RedisValue leaderboardKey,
        RedisValue entityKey,
        RankingType rankingType = RankingType.Default,
        CancellationToken cancellationToken = default)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);
        Guard.AgainstInvalidIdentityKey(entityKey);

        cancellationToken.ThrowIfCancellationRequested();
        ValidateRankingType(rankingType);

        if (rankingType == RankingType.Default)
        {
            var index = await this._redis.SortedSetRankAsync(CacheKey.ForLeaderboardSortedSet(leaderboardKey, this._options), entityKey);
            cancellationToken.ThrowIfCancellationRequested();
            return index.HasValue ? index.Value + 1 : null;
        }

        RedisKey[] keys = [CacheKey.ForLeaderboardSortedSet(leaderboardKey, this._options), CacheKey.ForUniqueScoreSortedSet(leaderboardKey, this._options)];
        RedisValue[] args = [entityKey, (int)rankingType];
        var result = await this._redis.ScriptEvaluateReadOnlyAsync(LeaderboardScript.ForEntityRank().ExecutableScript, keys, args);
        cancellationToken.ThrowIfCancellationRequested();
        return result.IsNull ? null : (long)result;
    }

    /// <summary>
    /// Returns number of entities currently stored in a leaderboard.
    /// </summary>
    /// <param name="leaderboardKey">Identifies leaderboard to query.</param>
    /// <param name="cancellationToken">Reserved for API symmetry. This implementation does not observe it after validation.</param>
    /// <returns>Total number of ranked members stored for <paramref name="leaderboardKey"/>.</returns>
    /// <remarks>Counts the authoritative ranking set, including legacy members whose metadata is absent.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="leaderboardKey"/> is missing.</exception>
    public async Task<long> GetSizeAsync(
        RedisValue leaderboardKey,
        CancellationToken cancellationToken = default)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);
        return await this._redis.SortedSetLengthAsync(CacheKey.ForLeaderboardSortedSet(leaderboardKey, this._options));
    }

    /// <summary>
    /// Deletes a single entity from a leaderboard.
    /// </summary>
    /// <param name="leaderboardKey">Identifies leaderboard that contains entity to remove.</param>
    /// <param name="entityKey">Identifies entity to delete.</param>
    /// <param name="cancellationToken">Cancels operation before Redis script execution begins.</param>
    /// <remarks>
    /// This method removes entity from ranking set, unique-score tracking set, and metadata hash in one script call.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="leaderboardKey"/> or <paramref name="entityKey"/> is missing.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="entityKey"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="entityKey"/> is non-positive.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled before script execution.</exception>
    public async Task DeleteEntityAsync(
        RedisValue leaderboardKey,
        RedisValue entityKey,
        CancellationToken cancellationToken = default)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);
        Guard.AgainstInvalidIdentityKey(entityKey);

        RedisKey[] keys =
        [
            CacheKey.ForLeaderboardSortedSet(leaderboardKey, this._options),
            CacheKey.ForUniqueScoreSortedSet(leaderboardKey, this._options),
            CacheKey.ForEntityDataHashSet(leaderboardKey, this._options)
        ];

        RedisValue[] args = [entityKey];

        var script = LeaderboardScript.ForDeleteEntitiesBatch().ExecutableScript;

        cancellationToken.ThrowIfCancellationRequested();
        await this._redis.ScriptEvaluateAsync(script, keys, args);
    }

    /// <summary>
    /// Deletes multiple entities from a leaderboard in one batch operation.
    /// </summary>
    /// <param name="leaderboardKey">Identifies leaderboard that contains entities to remove.</param>
    /// <param name="entityKeys">Keys for entities to delete. Collection must contain at least one value and no more than <see cref="DefaultMaxBatchOperationSize"/> items.</param>
    /// <param name="cancellationToken">Cancels operation before Redis script execution begins.</param>
    /// <remarks>
    /// Each entity key is validated before the Lua script runs. Script removes ranking entries, unique-score
    /// tracking entries, and metadata payloads for every supplied key.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="leaderboardKey"/> is missing or <paramref name="entityKeys"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="entityKeys"/> is empty or contains an empty key.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when batch size exceeds <see cref="DefaultMaxBatchOperationSize"/> or any entity key is non-positive.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled before script execution.</exception>
    public async Task DeleteEntitiesAsync(
        RedisValue leaderboardKey,
        IEnumerable<RedisValue> entityKeys,
        CancellationToken cancellationToken = default)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);
        Guard.AgainstNullOrEmptyCollection(entityKeys, nameof(entityKeys));

        var keysToDelete = entityKeys.ToArray();

        Guard.AgainstCollectionSizeExceeded(keysToDelete.Length, DefaultMaxBatchOperationSize, nameof(entityKeys));

        for (var i = 0; i < keysToDelete.Length; i++)
        {
            Guard.AgainstInvalidIdentityKey(keysToDelete[i]);
        }

        RedisKey[] keys =
        [
            CacheKey.ForLeaderboardSortedSet(leaderboardKey, this._options),
            CacheKey.ForUniqueScoreSortedSet(leaderboardKey, this._options),
            CacheKey.ForEntityDataHashSet(leaderboardKey, this._options)
        ];

        var script = LeaderboardScript.ForDeleteEntitiesBatch().ExecutableScript;

        cancellationToken.ThrowIfCancellationRequested();
        await this._redis.ScriptEvaluateAsync(script, keys, keysToDelete);
    }

    /// <summary>
    /// Deletes all Redis data associated with a leaderboard.
    /// </summary>
    /// <param name="leaderboardKey">Identifies leaderboard to delete.</param>
    /// <param name="cancellationToken">Reserved for API symmetry. This implementation does not observe it after validation.</param>
    /// <remarks>
    /// This operation removes ranking data, metadata, and auxiliary score tracking keys. Deleted data cannot be recovered by this library.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="leaderboardKey"/> is missing.</exception>
    public async Task DeleteAsync(
        RedisValue leaderboardKey,
        CancellationToken cancellationToken = default)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);

        RedisKey[] keys =
        [
            CacheKey.ForLeaderboardSortedSet(leaderboardKey, this._options),
            CacheKey.ForEntityDataHashSet(leaderboardKey, this._options),
            CacheKey.ForUniqueScoreSortedSet(leaderboardKey, this._options)
        ];

        await this._redis.KeyDeleteAsync(keys);
    }
    
    private async Task InternalUpdateEntityScoreAsync(
        RedisValue leaderboardKey,
        RedisValue entityKey,
        double score,
        bool fireAndForget,
        CancellationToken cancellationToken)
    {
        var invertedScore = -score;

        var commandFlags = fireAndForget ? CommandFlags.FireAndForget : CommandFlags.None;

        var script = LeaderboardScript.ForUpdateEntityScore().ExecutableScript;

        var keys = new[]
        {
            CacheKey.ForLeaderboardSortedSet(leaderboardKey, this._options),
            CacheKey.ForUniqueScoreSortedSet(leaderboardKey, this._options),
            CacheKey.ForEntityDataHashSet(leaderboardKey, this._options)
        };

        var args = new[] { entityKey, invertedScore };

        cancellationToken.ThrowIfCancellationRequested();
        await this._redis.ScriptEvaluateAsync(script, keys, args, commandFlags);
    }
    
    private async Task<TEntity[]> ReadEntitiesAsync(
        RedisValue leaderboardKey, string query, RedisValue first, RedisValue second, RankingType rankingType,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateRankingType(rankingType);

        RedisKey[] keys =
        [
            CacheKey.ForLeaderboardSortedSet(leaderboardKey, this._options),
            CacheKey.ForUniqueScoreSortedSet(leaderboardKey, this._options),
            CacheKey.ForEntityDataHashSet(leaderboardKey, this._options)
        ];
        RedisValue[] args = [query, (int)rankingType, first, second];
        var result = await this._redis.ScriptEvaluateReadOnlyAsync(LeaderboardScript.ForEntities().ExecutableScript, keys, args);
        cancellationToken.ThrowIfCancellationRequested();

        var rows = (RedisResult[])result ?? [];
        var entities = new TEntity[rows.Length];
        for (var i = 0; i < rows.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = (RedisResult[])rows[i];
            var member = (RedisValue)row[0];
            var rank = (long)row[1];
            var score = double.Parse((string)row[2], CultureInfo.InvariantCulture);
            var metadata = (byte[])row[3];
            var entity = metadata is { Length: > 0 }
                ? this._serializer.Deserialize<TEntity>(metadata)
                : new TEntity();
            entity.Rank = rank;
            EntityTypeAccessor<TEntity>.SetScore(entity, NormalizeScore(score));
            EntityTypeAccessor<TEntity>.SetKey(entity, member);
            entities[i] = entity;
        }
        return entities;
    }

    private static void ValidateRankingType(RankingType rankingType)
    {
        if (rankingType is not (RankingType.Default or RankingType.DenseRank or RankingType.StandardCompetition or RankingType.ModifiedCompetition))
            throw new KeyNotFoundException(
                $"Ranking type not found! Valid ranking types are: " +
                $"{string.Join(", ", Enum.GetValues(typeof(RankingType)).Cast<RankingType>().Select(t => $"{t} ({(int)t})"))}.");
    }

    private static double NormalizeScore(double score)
        => score < 0 ? -score : score;
}
