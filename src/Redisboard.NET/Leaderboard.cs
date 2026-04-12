using Redisboard.NET.Enumerations;
using Redisboard.NET.Helpers;
using Redisboard.NET.Interfaces;
using Redisboard.NET.Models;
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

    /// <summary>
    /// Initializes a new instance that uses an existing Redis database and serializer.
    /// </summary>
    /// <param name="redis">Redis database used for all leaderboard commands.</param>
    /// <param name="serializer">Serializes and deserializes entity metadata stored alongside ranking data.</param>
    /// <remarks>
    /// Use this overload when application code already controls creation of <see cref="IDatabase"/> and needs
    /// <see cref="Leaderboard{TEntity}"/> to reuse that database selection and connection lifetime.
    /// </remarks>
    public Leaderboard(IDatabase redis, ILeaderboardSerializer serializer)
    {
        this._redis = redis;
        this._serializer = serializer;
    }

    /// <summary>
    /// Initializes a new instance that resolves its Redis database from an <see cref="IConnectionMultiplexer"/>.
    /// </summary>
    /// <param name="connectionMultiplexer">Redis multiplexer used to resolve the target database.</param>
    /// <param name="serializer">Serializes and deserializes entity metadata stored alongside ranking data.</param>
    /// <param name="databaseIndex">Zero-based Redis database index passed to <see cref="IConnectionMultiplexer.GetDatabase(int, object)"/>.</param>
    /// <remarks>
    /// Use this overload when dependency injection or application startup manages the shared
    /// <see cref="IConnectionMultiplexer"/> and leaderboard instances should select their own database index.
    /// </remarks>
    public Leaderboard(
        IConnectionMultiplexer connectionMultiplexer,
        ILeaderboardSerializer serializer,
        int databaseIndex = 0)
        : this(connectionMultiplexer.GetDatabase(databaseIndex), serializer)
    {
    }

    /// <summary>
    /// Adds an entity to the specified leaderboard.
    /// </summary>
    /// <param name="leaderboardKey">Identifies leaderboard whose ranking and metadata structures should receive <paramref name="entity"/>.</param>
    /// <param name="entity">Entity to add. Its configured key and score properties supply the Redis member identifier and ranking score.</param>
    /// <param name="fireAndForget">When <see langword="true"/>, sends the write without waiting for Redis to acknowledge completion.</param>
    /// <param name="cancellationToken">Cancels operation before Redis script execution begins.</param>
    /// <remarks>
    /// <para>
    /// This method writes ranking data to the leaderboard sorted set, tracks distinct scores in the auxiliary
    /// score set, and stores serialized entity metadata in the leaderboard hash.
    /// </para>
    /// <para>
    /// Entity keys and scores are validated before serialization. Scores are negated before persistence so the
    /// highest score appears first when Redis returns ascending sorted set results.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="leaderboardKey"/> or entity key is missing.</exception>
    /// <exception cref="ArgumentException">Thrown when entity key is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when entity key is non-positive or entity score is negative.</exception>
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
            CacheKey.ForLeaderboardSortedSet(leaderboardKey),
            CacheKey.ForUniqueScoreSortedSet(leaderboardKey),
            CacheKey.ForEntityDataHashSet(leaderboardKey)
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
    /// <param name="entities">Entities to add. Each entry must provide a valid key and non-negative score.</param>
    /// <param name="fireAndForget">When <see langword="true"/>, sends the write without waiting for Redis to acknowledge completion.</param>
    /// <param name="cancellationToken">Cancels operation before Redis script execution begins.</param>
    /// <remarks>
    /// <para>
    /// Batch writes reduce Redis round trips by sending all entity triplets through one Lua script execution.
    /// </para>
    /// <para>
    /// The batch must contain at least one entity and no more than <see cref="DefaultMaxBatchOperationSize"/> items.
    /// Each entity is validated and serialized before the script runs.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="leaderboardKey"/> is missing or <paramref name="entities"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="entities"/> is empty or contains an entity with an empty key.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when batch size exceeds <see cref="DefaultMaxBatchOperationSize"/>, an entity key is non-positive, or an entity score is negative.</exception>
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
            CacheKey.ForLeaderboardSortedSet(leaderboardKey),
            CacheKey.ForUniqueScoreSortedSet(leaderboardKey),
            CacheKey.ForEntityDataHashSet(leaderboardKey)
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
    /// <param name="entity">Entity whose configured key identifies the stored member and whose score property contains the replacement score.</param>
    /// <param name="fireAndForget">When <see langword="true"/>, sends the write without waiting for Redis to acknowledge completion.</param>
    /// <param name="cancellationToken">Cancels operation before Redis script execution begins.</param>
    /// <remarks>
    /// This method updates ranking state only. Stored metadata remains unchanged until
    /// <see cref="UpdateEntityMetadataAsync(StackExchange.Redis.RedisValue,TEntity,bool,System.Threading.CancellationToken)"/> runs.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="leaderboardKey"/> or entity key is missing.</exception>
    /// <exception cref="ArgumentException">Thrown when entity key is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when entity key is non-positive or replacement score is negative.</exception>
    public async Task UpdateEntityScoreAsync(
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

        var invertedScore = -newScore;

        var commandFlags = fireAndForget ? CommandFlags.FireAndForget : CommandFlags.None;

        var script = LeaderboardScript.ForUpdateEntityScore().ExecutableScript;

        var keys = new[]
        {
            CacheKey.ForLeaderboardSortedSet(leaderboardKey),
            CacheKey.ForUniqueScoreSortedSet(leaderboardKey)
        };

        var args = new[] { entityKey, invertedScore };

        await this._redis.ScriptEvaluateAsync(script, keys, args, commandFlags);
    }

    /// <summary>
    /// Replaces the stored metadata for an existing entity without changing its score.
    /// </summary>
    /// <param name="leaderboardKey">Identifies leaderboard that contains <paramref name="entity"/>.</param>
    /// <param name="entity">Entity whose configured key identifies the stored member and whose serialized state becomes the new metadata payload.</param>
    /// <param name="fireAndForget">When <see langword="true"/>, sends the write without waiting for Redis to acknowledge completion.</param>
    /// <param name="cancellationToken">Cancels operation before Redis hash update begins.</param>
    /// <remarks>
    /// Ranking order does not change. Call
    /// <see cref="UpdateEntityScoreAsync(StackExchange.Redis.RedisValue,TEntity,bool,System.Threading.CancellationToken)"/> when score changes must also be persisted.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="leaderboardKey"/> or entity key is missing.</exception>
    /// <exception cref="ArgumentException">Thrown when entity key is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when entity key is non-positive.</exception>
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

        await this._redis.HashSetAsync(CacheKey.ForEntityDataHashSet(leaderboardKey), entityKey, metadata, flags: commandFlags);
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
    /// Window size adapts when target entity is near top of leaderboard so returned data never requests negative rank positions.
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

        var playerIndex = await this._redis.SortedSetRankAsync(
            CacheKey.ForLeaderboardSortedSet(leaderboardKey), entityKey);

        if (playerIndex == null) return [];

        var startIndex = Math.Max(playerIndex.Value - offset, 0);

        var pageSize = playerIndex.Value >= offset
            ? offset * 2 + 1
            : (int)playerIndex.Value + offset + 1;

        return await this.GetLeaderboardAsync(leaderboardKey, startIndex, pageSize, rankingType, cancellationToken);
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

        var invertedMaxScore = -minScore;
        var invertedMinScore = -maxScore;

        var entitiesInRange = await this._redis.SortedSetRangeByScoreAsync(
            CacheKey.ForLeaderboardSortedSet(leaderboardKey), invertedMinScore, invertedMaxScore);

        if (!entitiesInRange.Any()) return [];

        var startIndex = await this._redis.SortedSetRankAsync(
            CacheKey.ForLeaderboardSortedSet(leaderboardKey), entitiesInRange.First());

        var pageSize = entitiesInRange.Length;

        return await this.GetLeaderboardAsync(leaderboardKey, startIndex!.Value, pageSize, rankingType, cancellationToken);
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
        var pageSize = (int)(endRank - startRank) + 1;

        return await this.GetLeaderboardAsync(leaderboardKey, startIndex, pageSize, rankingType, cancellationToken);
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
            CacheKey.ForLeaderboardSortedSet(leaderboardKey), entityKey);

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
    /// Default ranking resolves directly from Redis rank indexes. Other ranking modes load the entity through
    /// the shared leaderboard read path so tie-aware rank semantics stay consistent across APIs.
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

        var playerIndex = await this._redis.SortedSetRankAsync(
            CacheKey.ForLeaderboardSortedSet(leaderboardKey), entityKey);

        if (playerIndex == null) return null;

        if (rankingType == RankingType.Default) return playerIndex.Value + 1;

        var entity = await this.GetLeaderboardAsync(leaderboardKey, playerIndex.Value, 1, rankingType, cancellationToken);

        return entity.First().Rank;
    }

    /// <summary>
    /// Returns number of entities currently stored in a leaderboard.
    /// </summary>
    /// <param name="leaderboardKey">Identifies leaderboard to query.</param>
    /// <param name="cancellationToken">Reserved for API symmetry. This implementation does not observe it after validation.</param>
    /// <returns>Total number of entities with metadata stored for <paramref name="leaderboardKey"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="leaderboardKey"/> is missing.</exception>
    public async Task<long> GetSizeAsync(
        RedisValue leaderboardKey,
        CancellationToken cancellationToken = default)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);
        return await this._redis.HashLengthAsync(CacheKey.ForEntityDataHashSet(leaderboardKey));
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
            CacheKey.ForLeaderboardSortedSet(leaderboardKey),
            CacheKey.ForUniqueScoreSortedSet(leaderboardKey),
            CacheKey.ForEntityDataHashSet(leaderboardKey)
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
            CacheKey.ForLeaderboardSortedSet(leaderboardKey),
            CacheKey.ForUniqueScoreSortedSet(leaderboardKey),
            CacheKey.ForEntityDataHashSet(leaderboardKey)
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
            CacheKey.ForLeaderboardSortedSet(leaderboardKey),
            CacheKey.ForEntityDataHashSet(leaderboardKey),
            CacheKey.ForUniqueScoreSortedSet(leaderboardKey)
        ];

        await this._redis.KeyDeleteAsync(keys);
    }

    private async Task<TEntity[]> GetLeaderboardAsync(
        RedisValue leaderboardKey, long startIndex, int pageSize, RankingType rankingType,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entityKeysWithRanking = rankingType switch
        {
            RankingType.Default
                => await this.GetEntityKeysWithDefaultRanking(leaderboardKey, startIndex, pageSize),
            RankingType.DenseRank
                => await this.GetEntityKeysWithDenseRanking(leaderboardKey, startIndex, pageSize),
            RankingType.ModifiedCompetition or RankingType.StandardCompetition
                => await this.GetEntityKeysWithCompetitionRanking(leaderboardKey, startIndex, pageSize, (int)rankingType),
            _ => throw new KeyNotFoundException(
                $"Ranking type not found! Valid ranking types are: " +
                $"{string.Join(", ", Enum.GetValues(typeof(RankingType)).Cast<RankingType>().Select(t => $"{t} ({(int)t})"))}.")
        };

        return await this.GetEntitiesDataAsync(leaderboardKey, entityKeysWithRanking, cancellationToken);
    }

    private async Task<Dictionary<RedisValue, LeaderboardStats>> GetEntityKeysWithDefaultRanking(
        RedisValue leaderboardKey, long startIndex, int pageSize)
    {
        var endIndex = startIndex + pageSize - 1;
        var result = new Dictionary<RedisValue, LeaderboardStats>();

        var entities = await this._redis.SortedSetRangeByRankWithScoresAsync(
            CacheKey.ForLeaderboardSortedSet(leaderboardKey), startIndex, endIndex);

        var rankForTopPlayer = startIndex + 1;

        for (var i = 0; i < entities.Length; i++)
            result.Add(entities[i].Element, new LeaderboardStats(rankForTopPlayer + i, entities[i].Score));

        return result;
    }

    private async Task<Dictionary<RedisValue, LeaderboardStats>> GetEntityKeysWithDenseRanking(
        RedisValue leaderboardKey, long startIndex, int pageSize)
    {
        var script = LeaderboardScript.ForEntityKeysByRangeWithDenseRank().ExecutableScript;

        RedisKey[] keys =
        [
            CacheKey.ForLeaderboardSortedSet(leaderboardKey),
            CacheKey.ForUniqueScoreSortedSet(leaderboardKey)
        ];

        RedisValue[] args = [startIndex, pageSize];

        return await this.EvaluateScriptToDictionaryAsync(script, keys, args);
    }

    private async Task<Dictionary<RedisValue, LeaderboardStats>> GetEntityKeysWithCompetitionRanking(
        RedisValue leaderboardKey, long startIndex, int pageSize, int rankingType)
    {
        var script = LeaderboardScript.ForEntityKeysByRangeWithCompetitionRank().ExecutableScript;

        RedisKey[] keys = [CacheKey.ForLeaderboardSortedSet(leaderboardKey)];

        RedisValue[] args = [startIndex, pageSize, rankingType];

        return await this.EvaluateScriptToDictionaryAsync(script, keys, args);
    }

    private async Task<Dictionary<RedisValue, LeaderboardStats>> EvaluateScriptToDictionaryAsync(
        string script, RedisKey[] keys, RedisValue[] args)
    {
        var results = await this._redis.ScriptEvaluateReadOnlyAsync(script, keys, args);

        return ((RedisResult[])results ?? [])
            .Select(r => (string[])r)
            .ToDictionary(r => new RedisValue(r[0]), r => new LeaderboardStats(long.Parse(r[1]), double.Parse(r[2])));
    }

    private async Task<TEntity[]> GetEntitiesDataAsync(
        RedisValue leaderboardKey,
        Dictionary<RedisValue, LeaderboardStats> keysWithLeaderboardMetrics,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var hashEntryKeys = keysWithLeaderboardMetrics.Select(p => p.Key).ToArray();

        var entityData = await this._redis.HashGetAsync(
            CacheKey.ForEntityDataHashSet(leaderboardKey), hashEntryKeys);

        var leaderboard = new TEntity[keysWithLeaderboardMetrics.Count];

        for (var i = 0; i < entityData.Length; i++)
        {
            var rawJson = entityData[i];
            var stats = keysWithLeaderboardMetrics[hashEntryKeys[i]];

            TEntity entity;

            if (rawJson.HasValue && !rawJson.IsNullOrEmpty)
            {
                entity = this._serializer.Deserialize<TEntity>((byte[])rawJson!);
            }
            else
            {
                // Entity was added without metadata — create empty instance and populate from Redis
                entity = new TEntity();
                EntityTypeAccessor<TEntity>.SetKey(entity, hashEntryKeys[i]);
                EntityTypeAccessor<TEntity>.SetScore(entity, NormalizeScore(stats.Score));
            }

            entity.Rank = stats.Rank;

            // Ensure score is the authoritative value from Redis (in case JSON is stale)
            EntityTypeAccessor<TEntity>.SetScore(entity, NormalizeScore(stats.Score));
            EntityTypeAccessor<TEntity>.SetKey(entity, hashEntryKeys[i]);

            leaderboard[i] = entity;
        }

        return leaderboard;
    }

    private static double NormalizeScore(double score)
        => score < 0 ? -score : score;
}
