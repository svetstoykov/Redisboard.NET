﻿using System.Text.Json;
using System.Transactions;
using Redisboard.NET.Enumerations;
using Redisboard.NET.Helpers;
using Redisboard.NET.Interfaces;
using Redisboard.NET.Models;
using StackExchange.Redis;

namespace Redisboard.NET;

/// <summary>
/// Represents a high-performance leaderboard implementation using Redis as the backend storage.
/// This class provides methods for adding, updating, and retrieving entities in a leaderboard,
/// supporting various ranking types including Default, Dense Rank, and Competition Ranking.
/// The leaderboard maintains ordering based on scores, with higher scores typically representing better performance.
/// </summary>
public class Leaderboard : ILeaderboard
{
    private readonly IDatabase _redis;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="Leaderboard"/> class with a specified Redis database.
    /// </summary>
    /// <param name="redis">The Redis database to use for leaderboard operations.</param>
    public Leaderboard(IDatabase redis)
    {
        _redis = redis;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Leaderboard"/> class with a specified Redis connection multiplexer and optional database index.
    /// </summary>
    /// <param name="connectionMultiplexer">The Redis connection multiplexer to use for creating the database connection.</param>
    /// <param name="databaseIndex">The index of the Redis database to use. Defaults to 0.</param>
    public Leaderboard(IConnectionMultiplexer connectionMultiplexer, int databaseIndex = 0)
        : this(connectionMultiplexer.GetDatabase(databaseIndex))
    {
    }

    /// <inheritdoc />
    public async Task AddEntityAsync(
        RedisValue leaderboardKey, RedisValue entityKey, RedisValue metadata = default, bool fireAndForget = false)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);
        Guard.AgainstInvalidIdentityKey(entityKey);

        var commandFlags = fireAndForget
            ? CommandFlags.FireAndForget
            : CommandFlags.None;

        var transaction = _redis.CreateTransaction();

        const double initialScore = default;

        transaction.SortedSetAddAsync(CacheKey.ForLeaderboardSortedSet(leaderboardKey), entityKey, initialScore, commandFlags);

        transaction.SortedSetAddAsync(CacheKey.ForUniqueScoreSortedSet(leaderboardKey), initialScore, initialScore, commandFlags);

        if (metadata != default)
            transaction.HashSetAsync(CacheKey.ForEntityDataHashSet(leaderboardKey), entityKey, metadata, flags: commandFlags);

        await TryExecuteTransactionAsync(transaction, "Failed to add entities to leaderboard!");
    }

    /// <inheritdoc />
    public async Task UpdateEntityScoreAsync(
        RedisValue leaderboardKey, RedisValue entityKey, double newScore, bool fireAndForget = false)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);
        Guard.AgainstInvalidIdentityKey(entityKey);
        Guard.AgainstInvalidScore(newScore);

        var invertedScore = -newScore;

        var commandFlags = fireAndForget
            ? CommandFlags.FireAndForget
            : CommandFlags.None;

        var script = LeaderboardScript.ForUpdateEntityScore().ExecutableScript;

        var keys = new[]
        {
            CacheKey.ForLeaderboardSortedSet(leaderboardKey),
            CacheKey.ForUniqueScoreSortedSet(leaderboardKey)
        };

        var values = new[] { entityKey, invertedScore };

        await _redis.ScriptEvaluateAsync(script, keys, values, commandFlags);
    }

    /// <inheritdoc />
    public async Task UpdateEntityMetadataAsync(
        RedisValue leaderboardKey, RedisValue entityKey, RedisValue metadata, bool fireAndForget = false)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);
        Guard.AgainstInvalidIdentityKey(entityKey);
        Guard.AgainstInvalidMetadata(metadata);

        var commandFlags = fireAndForget
            ? CommandFlags.FireAndForget
            : CommandFlags.None;

        await _redis.HashSetAsync(CacheKey.ForEntityDataHashSet(leaderboardKey), entityKey, metadata, flags: commandFlags);
    }

    /// <inheritdoc />
    public async Task<ILeaderboardEntity[]> GetEntityAndNeighboursAsync(
        RedisValue leaderboardKey, RedisValue entityKey, int offset = 10, RankingType rankingType = RankingType.Default)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);
        Guard.AgainstInvalidIdentityKey(entityKey);
        Guard.AgainstInvalidOffset(offset);

        var playerIndex = await _redis.SortedSetRankAsync(CacheKey.ForLeaderboardSortedSet(leaderboardKey), entityKey);

        if (playerIndex == null) return [];

        var startIndex = Math.Max(playerIndex.Value - offset, 0);

        var pageSize = playerIndex.Value > offset
            ? offset * 2
            : (int)playerIndex.Value + offset;

        return await GetLeaderboardAsync(leaderboardKey, startIndex, pageSize, rankingType);
    }

    /// <inheritdoc />
    public async Task<ILeaderboardEntity[]> GetEntitiesByScoreRangeAsync(
        RedisValue leaderboardKey,
        double minScore,
        double maxScore,
        RankingType rankingType = RankingType.Default)
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

        var startIndex = await _redis.SortedSetRankAsync(CacheKey.ForLeaderboardSortedSet(leaderboardKey), entitiesInRange.First());

        var pageSize = entitiesInRange.Length - 1;

        return await GetLeaderboardAsync(leaderboardKey, startIndex!.Value, pageSize, rankingType);
    }

    /// <inheritdoc />
    public async Task<RedisValue> GetEntityMetadataAsync(RedisValue leaderboardKey, RedisValue entityKey)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);
        Guard.AgainstInvalidIdentityKey(entityKey);

        return await _redis.HashGetAsync(CacheKey.ForEntityDataHashSet(leaderboardKey), entityKey);
    }

    /// <inheritdoc />
    public async Task<double?> GetEntityScoreAsync(RedisValue leaderboardKey, RedisValue entityKey)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);
        Guard.AgainstInvalidIdentityKey(entityKey);

        var score = await _redis.SortedSetScoreAsync(
            CacheKey.ForLeaderboardSortedSet(leaderboardKey),
            entityKey);

        return score.HasValue ? NormalizeScore(score.Value) : null;
    }

    /// <inheritdoc />
    public async Task<long?> GetEntityRankAsync(RedisValue leaderboardKey, RedisValue entityKey, RankingType rankingType = RankingType.Default)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);
        Guard.AgainstInvalidIdentityKey(entityKey);

        const int singleUserPageSize = 1;

        var playerIndex = await _redis.SortedSetRankAsync(CacheKey.ForLeaderboardSortedSet(leaderboardKey), entityKey);

        if (playerIndex == null) return null;

        if (rankingType == RankingType.Default) return playerIndex.Value + 1;

        var entity = await GetLeaderboardAsync(leaderboardKey, playerIndex.Value, singleUserPageSize, rankingType);

        return entity.First().Rank;
    }

    /// <inheritdoc />
    public async Task DeleteEntityAsync(RedisValue leaderboardKey, RedisValue entityKey)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);
        Guard.AgainstInvalidIdentityKey(entityKey);

        var transaction = _redis.CreateTransaction();

        transaction.SortedSetRemoveAsync(CacheKey.ForLeaderboardSortedSet(leaderboardKey), entityKey);

        transaction.HashDeleteAsync(CacheKey.ForEntityDataHashSet(leaderboardKey), entityKey);

        transaction.SortedSetRemoveAsync(CacheKey.ForUniqueScoreSortedSet(leaderboardKey), entityKey);

        await TryExecuteTransactionAsync(transaction, "Failed to delete entity from leaderboard!");
    }

    /// <inheritdoc />
    public async Task<long> GetSizeAsync(RedisValue leaderboardKey)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);

        return await _redis.HashLengthAsync(CacheKey.ForEntityDataHashSet(leaderboardKey));
    }

    /// <inheritdoc />
    public async Task DeleteAsync(RedisValue leaderboardKey)
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

    private async Task<ILeaderboardEntity[]> GetLeaderboardAsync(
        RedisValue leaderboardKey, long startIndex, int pageSize, RankingType rankingType)
    {
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
                $"{string.Join(", ", Enum.GetValues(typeof(RankingType)).Cast<RankingType>().Select(type => $"{type} ({(int)type})"))}.")
        };

        return await GetEntitiesDataAsync(leaderboardKey, entityKeysWithRanking);
    }

    private async Task<Dictionary<RedisValue, LeaderboardStats>> GetEntityKeysWithDefaultRanking(
        RedisValue leaderboardKey, long startIndex, int pageSize)
    {
        var endIndex = startIndex + pageSize;

        var entityKeysWithRanking = new Dictionary<RedisValue, LeaderboardStats>();

        var entities = await _redis.SortedSetRangeByRankWithScoresAsync(
            CacheKey.ForLeaderboardSortedSet(leaderboardKey), startIndex, endIndex);

        var rankForTopPlayer = startIndex + 1;
        
        for (var i = 0; i < entities.Length; i++)
            entityKeysWithRanking.Add(entities[i].Element, new LeaderboardStats(rankForTopPlayer + i, entities[i].Score));

        return entityKeysWithRanking;
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
        var results = await _redis.ScriptEvaluateReadOnlyAsync(script, keys.ToArray(), args.ToArray());

        return ((RedisResult[])results ?? [])
            .Select(r => (string[])r)
            .ToDictionary(r => new RedisValue(r[0]), r => new LeaderboardStats(long.Parse(r[1]), double.Parse(r[2])));
    }

    private async Task<ILeaderboardEntity[]> GetEntitiesDataAsync(
        RedisValue leaderboardKey,
        Dictionary<RedisValue, LeaderboardStats> keysWithLeaderboardMetrics)
    {
        var hashEntryKeys = keysWithLeaderboardMetrics
            .Select(p => p.Key)
            .ToArray();

        var entityData = await _redis.HashGetAsync(CacheKey.ForEntityDataHashSet(leaderboardKey), hashEntryKeys);

        var leaderboard = new ILeaderboardEntity[keysWithLeaderboardMetrics.Count];

        for (var i = 0; i < entityData.Length; i++)
        {
            ILeaderboardEntity entity = new LeaderboardEntryWrapper();

            entity.Key = hashEntryKeys[i];
            entity.Rank = keysWithLeaderboardMetrics[entity.Key].Rank;
            entity.Score = NormalizeScore(keysWithLeaderboardMetrics[entity.Key].Score);
            entity.Metadata = entityData[i];

            leaderboard[i] = entity;
        }

        return leaderboard;
    }

    private static async Task TryExecuteTransactionAsync(
        ITransaction transaction,
        string errorMessage = "Failed to execute transaction!")
    {
        var commited = await transaction.ExecuteAsync();

        if (!commited) throw new TransactionException(errorMessage);
    }

    private static double NormalizeScore(double score)
        => score < 0 ? -score : score;
}