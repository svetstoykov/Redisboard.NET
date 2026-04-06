using System.Transactions;
using Redisboard.NET.Enumerations;
using Redisboard.NET.Helpers;
using Redisboard.NET.Interfaces;
using Redisboard.NET.Models;
using ILeaderboardSerializer = Redisboard.NET.Serialization.ILeaderboardSerializer;
using MemoryPackLeaderboardSerializer = Redisboard.NET.Serialization.MemoryPackLeaderboardSerializer;
using StackExchange.Redis;
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

namespace Redisboard.NET;

/// <inheritdoc cref="ILeaderboard{TEntity}"/>
public class Leaderboard<TEntity> : ILeaderboard<TEntity>
    where TEntity : ILeaderboardEntity, new()
{
    private readonly IDatabase _redis;
    private readonly ILeaderboardSerializer _serializer;

    /// <summary>
    /// Initializes a new instance with the specified Redis database and optional serializer.
    /// </summary>
    /// <param name="redis">The Redis database to use.</param>
    /// <param name="serializer">
    /// Serializer used for entity metadata persistence.
    /// Defaults to <see cref="MemoryPackLeaderboardSerializer"/>.
    /// </param>
    public Leaderboard(IDatabase redis, ILeaderboardSerializer serializer)
    {
        _redis = redis;
        _serializer = serializer;
    }

    /// <summary>
    /// Initializes a new instance using an <see cref="IConnectionMultiplexer"/> and optional database index.
    /// </summary>
    /// <param name="connectionMultiplexer">The Redis connection multiplexer.</param>
    /// <param name="databaseIndex">The Redis database index to use (0 by default).</param>
    /// <param name="serializer">
    /// Serializer used for entity metadata persistence.
    /// Defaults to <see cref="MemoryPackLeaderboardSerializer"/>.
    /// </param>
    public Leaderboard(
        IConnectionMultiplexer connectionMultiplexer,
        ILeaderboardSerializer serializer,
        int databaseIndex = 0)
        : this(connectionMultiplexer.GetDatabase(databaseIndex), serializer)
    {
    }

    /// <inheritdoc />
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

        var metadata = _serializer.Serialize(entity);

        var invertedScore = -score;

        var commandFlags = fireAndForget ? CommandFlags.FireAndForget : CommandFlags.None;

        var transaction = _redis.CreateTransaction();

        transaction.SortedSetAddAsync(CacheKey.ForLeaderboardSortedSet(leaderboardKey), entityKey, invertedScore, commandFlags);
        transaction.SortedSetAddAsync(CacheKey.ForUniqueScoreSortedSet(leaderboardKey), invertedScore, invertedScore, commandFlags);
        transaction.HashSetAsync(CacheKey.ForEntityDataHashSet(leaderboardKey), entityKey, metadata, flags: commandFlags);

        await TryExecuteTransactionAsync(transaction, cancellationToken, "Failed to add entity to leaderboard!");
    }

    /// <inheritdoc />
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

        await _redis.ScriptEvaluateAsync(script, keys, args, commandFlags);
    }

    /// <inheritdoc />
    public async Task UpdateEntityMetadataAsync(
        RedisValue leaderboardKey,
        TEntity entity,
        bool fireAndForget = false,
        CancellationToken cancellationToken = default)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);

        var entityKey = EntityTypeAccessor<TEntity>.GetKey(entity);
        Guard.AgainstInvalidIdentityKey(entityKey);

        var metadata = _serializer.Serialize(entity);

        var commandFlags = fireAndForget ? CommandFlags.FireAndForget : CommandFlags.None;

        await _redis.HashSetAsync(CacheKey.ForEntityDataHashSet(leaderboardKey), entityKey, metadata, flags: commandFlags);
    }

    /// <inheritdoc />
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

        var playerIndex = await _redis.SortedSetRankAsync(
            CacheKey.ForLeaderboardSortedSet(leaderboardKey), entityKey);

        if (playerIndex == null) return [];

        var startIndex = Math.Max(playerIndex.Value - offset, 0);

        var pageSize = playerIndex.Value >= offset
            ? offset * 2 + 1
            : (int)playerIndex.Value + offset + 1;

        return await GetLeaderboardAsync(leaderboardKey, startIndex, pageSize, rankingType, cancellationToken);
    }

    /// <inheritdoc />
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

        var entitiesInRange = await _redis.SortedSetRangeByScoreAsync(
            CacheKey.ForLeaderboardSortedSet(leaderboardKey), invertedMinScore, invertedMaxScore);

        if (!entitiesInRange.Any()) return [];

        var startIndex = await _redis.SortedSetRankAsync(
            CacheKey.ForLeaderboardSortedSet(leaderboardKey), entitiesInRange.First());

        var pageSize = entitiesInRange.Length;

        return await GetLeaderboardAsync(leaderboardKey, startIndex!.Value, pageSize, rankingType, cancellationToken);
    }

    /// <inheritdoc />
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

        return await GetLeaderboardAsync(leaderboardKey, startIndex, pageSize, rankingType, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<double?> GetEntityScoreAsync(
        RedisValue leaderboardKey,
        RedisValue entityKey,
        CancellationToken cancellationToken = default)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);
        Guard.AgainstInvalidIdentityKey(entityKey);

        var score = await _redis.SortedSetScoreAsync(
            CacheKey.ForLeaderboardSortedSet(leaderboardKey), entityKey);

        return score.HasValue ? NormalizeScore(score.Value) : null;
    }

    /// <inheritdoc />
    public async Task<long?> GetEntityRankAsync(
        RedisValue leaderboardKey,
        RedisValue entityKey,
        RankingType rankingType = RankingType.Default,
        CancellationToken cancellationToken = default)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);
        Guard.AgainstInvalidIdentityKey(entityKey);

        var playerIndex = await _redis.SortedSetRankAsync(
            CacheKey.ForLeaderboardSortedSet(leaderboardKey), entityKey);

        if (playerIndex == null) return null;

        if (rankingType == RankingType.Default) return playerIndex.Value + 1;

        var entity = await GetLeaderboardAsync(leaderboardKey, playerIndex.Value, 1, rankingType, cancellationToken);

        return entity.First().Rank;
    }

    /// <inheritdoc />
    public async Task<long> GetSizeAsync(
        RedisValue leaderboardKey,
        CancellationToken cancellationToken = default)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);
        return await _redis.HashLengthAsync(CacheKey.ForEntityDataHashSet(leaderboardKey));
    }

    /// <inheritdoc />
    public async Task DeleteEntityAsync(
        RedisValue leaderboardKey,
        RedisValue entityKey,
        CancellationToken cancellationToken = default)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);
        Guard.AgainstInvalidIdentityKey(entityKey);

        var transaction = _redis.CreateTransaction();

        transaction.SortedSetRemoveAsync(CacheKey.ForLeaderboardSortedSet(leaderboardKey), entityKey);
        transaction.HashDeleteAsync(CacheKey.ForEntityDataHashSet(leaderboardKey), entityKey);
        transaction.SortedSetRemoveAsync(CacheKey.ForUniqueScoreSortedSet(leaderboardKey), entityKey);

        await TryExecuteTransactionAsync(transaction, cancellationToken, "Failed to delete entity from leaderboard!");
    }

    /// <inheritdoc />
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

        await _redis.KeyDeleteAsync(keys);
    }

    // ---- Private helpers ---------------------------------------------------

    private async Task<TEntity[]> GetLeaderboardAsync(
        RedisValue leaderboardKey, long startIndex, int pageSize, RankingType rankingType,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entityKeysWithRanking = rankingType switch
        {
            RankingType.Default
                => await GetEntityKeysWithDefaultRanking(leaderboardKey, startIndex, pageSize),
            RankingType.DenseRank
                => await GetEntityKeysWithDenseRanking(leaderboardKey, startIndex, pageSize),
            RankingType.ModifiedCompetition or RankingType.StandardCompetition
                => await GetEntityKeysWithCompetitionRanking(leaderboardKey, startIndex, pageSize, (int)rankingType),
            _ => throw new KeyNotFoundException(
                $"Ranking type not found! Valid ranking types are: " +
                $"{string.Join(", ", Enum.GetValues(typeof(RankingType)).Cast<RankingType>().Select(t => $"{t} ({(int)t})"))}.")
        };

        return await GetEntitiesDataAsync(leaderboardKey, entityKeysWithRanking, cancellationToken);
    }

    private async Task<Dictionary<RedisValue, LeaderboardStats>> GetEntityKeysWithDefaultRanking(
        RedisValue leaderboardKey, long startIndex, int pageSize)
    {
        var endIndex = startIndex + pageSize - 1;
        var result = new Dictionary<RedisValue, LeaderboardStats>();

        var entities = await _redis.SortedSetRangeByRankWithScoresAsync(
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

        return await EvaluateScriptToDictionaryAsync(script, keys, args);
    }

    private async Task<Dictionary<RedisValue, LeaderboardStats>> GetEntityKeysWithCompetitionRanking(
        RedisValue leaderboardKey, long startIndex, int pageSize, int rankingType)
    {
        var script = LeaderboardScript.ForEntityKeysByRangeWithCompetitionRank().ExecutableScript;

        RedisKey[] keys = [CacheKey.ForLeaderboardSortedSet(leaderboardKey)];

        RedisValue[] args = [startIndex, pageSize, rankingType];

        return await EvaluateScriptToDictionaryAsync(script, keys, args);
    }

    private async Task<Dictionary<RedisValue, LeaderboardStats>> EvaluateScriptToDictionaryAsync(
        string script, RedisKey[] keys, RedisValue[] args)
    {
        var results = await _redis.ScriptEvaluateReadOnlyAsync(script, keys, args);

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

        var entityData = await _redis.HashGetAsync(
            CacheKey.ForEntityDataHashSet(leaderboardKey), hashEntryKeys);

        var leaderboard = new TEntity[keysWithLeaderboardMetrics.Count];

        for (var i = 0; i < entityData.Length; i++)
        {
            var rawJson = entityData[i];
            var stats = keysWithLeaderboardMetrics[hashEntryKeys[i]];

            TEntity entity;

            if (rawJson.HasValue && !rawJson.IsNullOrEmpty)
            {
                entity = _serializer.Deserialize<TEntity>((byte[])rawJson!);
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

    private static async Task TryExecuteTransactionAsync(
        ITransaction transaction,
        CancellationToken cancellationToken = default,
        string errorMessage = "Failed to execute transaction!")
    {
        cancellationToken.ThrowIfCancellationRequested();
        var committed = await transaction.ExecuteAsync();
        if (!committed) throw new TransactionException(errorMessage);
    }

    private static double NormalizeScore(double score)
        => score < 0 ? -score : score;
}
