using System.Text.Json;
using System.Transactions;
using Redisboard.NET.Enumerations;
using Redisboard.NET.Helpers;
using Redisboard.NET.Interfaces;
using Redisboard.NET.Models;
using StackExchange.Redis;

namespace Redisboard.NET;

public class Leaderboard : ILeaderboard
{
    private readonly IDatabase _redis;

    public Leaderboard(IDatabase redis)
    {
        _redis = redis;
    }

    public Leaderboard(IConnectionMultiplexer connectionMultiplexer, int databaseIndex = 0)
        : this(connectionMultiplexer.GetDatabase(databaseIndex))
    {
    }

    public async Task AddEntityAsync(
        RedisValue leaderboardKey,
        ILeaderboardEntity entity,
        bool fireAndForget = false)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);
        Guard.AgainstInvalidLeaderboardEntities(entity);

        var commandFlags = fireAndForget
            ? CommandFlags.FireAndForget
            : CommandFlags.None;

        var transaction = _redis.CreateTransaction();

        const double initialScore = default;

        transaction.SortedSetAddAsync(
            CacheKey.ForLeaderboardSortedSet(leaderboardKey),
            entity.Key,
            initialScore,
            commandFlags);

        transaction.SortedSetAddAsync(
            CacheKey.ForUniqueScoreSortedSet(leaderboardKey),
            initialScore,
            initialScore,
            commandFlags);

        transaction.HashSetAsync(
            CacheKey.ForEntityDataHashSet(leaderboardKey),
            entity.Key,
            JsonSerializer.Serialize(entity.Metadata),
            flags: commandFlags);

        await TryExecuteTransactionAsync(
            transaction, "Failed to add entities to leaderboard!");
    }

    public async Task UpdateEntityScoreAsync(
        RedisValue leaderboardKey,
        RedisValue entityKey,
        double newScore,
        bool fireAndForget = false)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);
        Guard.AgainstInvalidIdentityKey(entityKey);
        Guard.AgainstInvalidScore(newScore);

        var invertedScore = -newScore;

        var commandFlags = fireAndForget
            ? CommandFlags.FireAndForget
            : CommandFlags.None;

        var transaction = _redis.CreateTransaction();

        transaction.SortedSetAddAsync(
            CacheKey.ForLeaderboardSortedSet(leaderboardKey),
            entityKey,
            invertedScore,
            commandFlags);

        transaction.SortedSetAddAsync(
            CacheKey.ForUniqueScoreSortedSet(leaderboardKey),
            invertedScore,
            invertedScore,
            commandFlags);

        await TryExecuteTransactionAsync(
            transaction, "Failed to update entity score in leaderboard!");
    }

    public async Task<ILeaderboardEntity[]> GetEntityAndNeighboursAsync(
        RedisValue leaderboardKey,
        RedisValue entityKey,
        int offset = 10,
        RankingType rankingType = RankingType.Default)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);
        Guard.AgainstInvalidIdentityKey(entityKey);
        Guard.AgainstInvalidOffset(offset);

        var playerIndex = await _redis
            .SortedSetRankAsync(
                CacheKey.ForLeaderboardSortedSet(leaderboardKey),
                entityKey);

        if (playerIndex == null)
        {
            return [];
        }

        var startIndex = Math.Max(playerIndex.Value - offset, 0);

        var pageSize = playerIndex.Value > offset
            ? offset * 2
            : (int)playerIndex.Value + offset;

        return await GetLeaderboardAsync(
            leaderboardKey, startIndex, pageSize, rankingType);
    }

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

        var entitiesInRange = await _redis
            .SortedSetRangeByScoreAsync(
                CacheKey.ForLeaderboardSortedSet(leaderboardKey),
                invertedMinScore,
                invertedMaxScore);

        if (!entitiesInRange.Any())
        {
            return [];
        }

        var startIndex = await _redis.SortedSetRankAsync(
            CacheKey.ForLeaderboardSortedSet(leaderboardKey),
            entitiesInRange.First());

        var pageSize = entitiesInRange.Length - 1;

        return await GetLeaderboardAsync(
            leaderboardKey, startIndex!.Value, pageSize, rankingType);
    }

    public async Task<RedisValue> GetEntityMetadataDataAsync(RedisValue leaderboardKey, RedisValue entityKey)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);
        Guard.AgainstInvalidIdentityKey(entityKey);
        
        var hashEntry = await _redis.HashGetAsync(
            CacheKey.ForEntityDataHashSet(leaderboardKey),
            entityKey);
        
        if(hashEntry == default) return default;
        
        return hashEntry;
    }

    public async Task<double?> GetEntityScoreAsync(RedisValue leaderboardKey, RedisValue entityKey)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);
        Guard.AgainstInvalidIdentityKey(entityKey);

        var score = await _redis.SortedSetScoreAsync(
            CacheKey.ForLeaderboardSortedSet(leaderboardKey),
            entityKey);

        return score.HasValue
            ? NormalizeScore(score.Value)
            : null;
    }

    public async Task<long?> GetEntityRankAsync(
        RedisValue leaderboardKey,
        RedisValue entityKey,
        RankingType rankingType = RankingType.Default)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);
        Guard.AgainstInvalidIdentityKey(entityKey);

        const int singleUserPageSize = 1;

        var playerIndex = await _redis
            .SortedSetRankAsync(
                CacheKey.ForLeaderboardSortedSet(leaderboardKey),
                entityKey);

        if (playerIndex == null)
        {
            return null;
        }

        if (rankingType == RankingType.Default)
        {
            return playerIndex.Value + 1;
        }

        var entity = await GetLeaderboardAsync(
            leaderboardKey, playerIndex.Value, singleUserPageSize, rankingType);

        return entity.First().Rank;
    }

    public async Task DeleteEntityAsync(RedisValue leaderboardKey, RedisValue entityKey)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);
        Guard.AgainstInvalidIdentityKey(entityKey);

        var transaction = _redis.CreateTransaction();

        transaction.SortedSetRemoveAsync(
            CacheKey.ForLeaderboardSortedSet(leaderboardKey),
            entityKey);

        transaction.HashDeleteAsync(
            CacheKey.ForEntityDataHashSet(leaderboardKey),
            entityKey);

        transaction.SortedSetRemoveAsync(
            CacheKey.ForUniqueScoreSortedSet(leaderboardKey),
            entityKey);

        await TryExecuteTransactionAsync(transaction, "Failed to delete entity from leaderboard!");
    }

    public async Task<long> GetSizeAsync(RedisValue leaderboardKey)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);

        return await _redis.HashLengthAsync(CacheKey.ForEntityDataHashSet(leaderboardKey));
    }

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
                => await GetEntityKeysWithDefaultRanking(
                    leaderboardKey, startIndex, pageSize),
            RankingType.DenseRank
                => await GetEntityKeysWithDenseRanking(
                    leaderboardKey, startIndex, pageSize),
            RankingType.ModifiedCompetition or RankingType.StandardCompetition
                => await GetEntityKeysWithCompetitionRanking(
                    leaderboardKey, startIndex, pageSize, (int)rankingType),
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
            CacheKey.ForLeaderboardSortedSet(leaderboardKey),
            startIndex,
            endIndex);

        var rankForTopPlayer = startIndex + 1;
        for (var i = 0; i < entities.Length; i++)
        {
            entityKeysWithRanking.Add(
                entities[i].Element,
                new LeaderboardStats(rankForTopPlayer + i, entities[i].Score));
        }

        return entityKeysWithRanking;
    }

    private async Task<Dictionary<RedisValue, LeaderboardStats>> GetEntityKeysWithDenseRanking(
        RedisValue leaderboardKey, long startIndex, int pageSize)
    {
        var script = LuaScript
            .Prepare(LeaderboardScript.ForEntityKeysByRangeWithDenseRank())
            .ExecutableScript;

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
        var script = LuaScript
            .Prepare(LeaderboardScript.ForEntityKeysByRangeWithCompetitionRank())
            .ExecutableScript;

        RedisKey[] keys =
        [
            CacheKey.ForLeaderboardSortedSet(leaderboardKey)
        ];

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

        var entityData = await _redis.HashGetAsync(
            CacheKey.ForEntityDataHashSet(leaderboardKey),
            hashEntryKeys);

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

        if (!commited)
            throw new TransactionException(errorMessage);
    }

    private static double NormalizeScore(double score)
        => score < 0 ? -score : score;
}