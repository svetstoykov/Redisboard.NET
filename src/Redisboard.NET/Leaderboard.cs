using System.Text.Json;
using System.Transactions;
using Redisboard.NET.Enumerations;
using Redisboard.NET.Helpers;
using Redisboard.NET.Interfaces;
using Redisboard.NET.Models;
using StackExchange.Redis;

namespace Redisboard.NET;

public class Leaderboard<TEntity> : ILeaderboard<TEntity>
    where TEntity : ILeaderboardEntity
{
    private readonly IDatabase _redis;

    public Leaderboard(IDatabase redis)
    {
        _redis = redis;
    }

    public Leaderboard(IConnectionMultiplexer connectionMultiplexer)
        : this(connectionMultiplexer.GetDatabase())
    {
    }
    
    public async Task AddEntitiesAsync(
        RedisValue leaderboardKey,
        TEntity[] entities,
        bool fireAndForget = false)
    {
        Guard.AgainstInvalidLeaderboardKey(leaderboardKey);
        Guard.AgainstInvalidLeaderboardEntities<TEntity>(entities);

        var (sortedSetEntries, hashSetEntries, uniqueScoreEntries) =
            PrepareEntitiesForLeaderboardAddOperation(entities);

        var commandFlags = fireAndForget
            ? CommandFlags.FireAndForget
            : CommandFlags.None;

        var transaction = _redis.CreateTransaction();

        transaction.SortedSetAddAsync(
            CacheKey.ForLeaderboardSortedSet(leaderboardKey),
            sortedSetEntries,
            commandFlags);

        transaction.HashSetAsync(
            CacheKey.ForEntityDataHashSet(leaderboardKey),
            hashSetEntries,
            commandFlags);

        transaction.SortedSetAddAsync(
            CacheKey.ForUniqueScoreSortedSet(leaderboardKey),
            uniqueScoreEntries,
            commandFlags);

        await TryExecuteTransactionAsync(
            transaction, "Failed to add entities to leaderboard!");
    }
    
    public async Task UpdateEntityScoreAsync(
        RedisValue leaderboardKey,
        RedisValue entityKey,
        double newScore,
        bool fireAndForget = false)
    {
        Guard.AgainstInvalidLeaderboardKey(leaderboardKey);
        Guard.AgainstInvalidEntityKey(entityKey);
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
            entityKey,
            invertedScore,
            commandFlags);

        await TryExecuteTransactionAsync(
            transaction, "Failed to update entity score in leaderboard!");
    }
    
    public async Task<TEntity[]> GetEntityAndNeighboursAsync(
        RedisValue leaderboardKey,
        RedisValue entityKey,
        int offset = 10,
        RankingType rankingType = RankingType.Default)
    {
        Guard.AgainstInvalidLeaderboardKey(leaderboardKey);
        Guard.AgainstInvalidEntityKey(entityKey);
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

    public async Task<TEntity[]> GetEntitiesByScoreRangeAsync(
        RedisValue leaderboardKey,
        double minScore,
        double maxScore,
        RankingType rankingType = RankingType.Default)
    {
        Guard.AgainstInvalidLeaderboardKey(leaderboardKey);
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

    public async Task<TEntity> GetEntityDataAsync(RedisValue leaderboardKey, RedisValue entityKey)
    {
        Guard.AgainstInvalidLeaderboardKey(leaderboardKey);
        Guard.AgainstInvalidEntityKey(entityKey);
        
        var hashEntry = await _redis.HashGetAsync(
            CacheKey.ForEntityDataHashSet(leaderboardKey),
            entityKey);
        
        if(hashEntry == default) return default;

        var entity = JsonSerializer.Deserialize<TEntity>(hashEntry);

        entity.Score = await GetEntityScoreAsync(leaderboardKey, entityKey) ?? default;

        return entity;
    }

    public async Task<double?> GetEntityScoreAsync(RedisValue leaderboardKey, RedisValue entityKey)
    {
        Guard.AgainstInvalidLeaderboardKey(leaderboardKey);
        Guard.AgainstInvalidEntityKey(entityKey);
        
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
        Guard.AgainstInvalidLeaderboardKey(leaderboardKey);
        Guard.AgainstInvalidEntityKey(entityKey);
        
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
        Guard.AgainstInvalidLeaderboardKey(leaderboardKey);
        Guard.AgainstInvalidEntityKey(entityKey);

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
        Guard.AgainstInvalidLeaderboardKey(leaderboardKey);

        return await _redis.HashLengthAsync(CacheKey.ForEntityDataHashSet(leaderboardKey));
    }

    public async Task DeleteAsync(RedisValue leaderboardKey)
    {
        Guard.AgainstInvalidLeaderboardKey(leaderboardKey);

        RedisKey[] keys =
        [
            CacheKey.ForLeaderboardSortedSet(leaderboardKey),
            CacheKey.ForEntityDataHashSet(leaderboardKey),
            CacheKey.ForUniqueScoreSortedSet(leaderboardKey)
        ];

        await _redis.KeyDeleteAsync(keys);
    }

    private async Task<TEntity[]> GetLeaderboardAsync(
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

    private async Task<TEntity[]> GetEntitiesDataAsync(
        RedisValue leaderboardKey,
        Dictionary<RedisValue, LeaderboardStats> keysWithLeaderboardMetrics)
    {
        var hashEntryKeys = keysWithLeaderboardMetrics
            .Select(p => p.Key)
            .ToArray();

        var entityData = await _redis.HashGetAsync(
            CacheKey.ForEntityDataHashSet(leaderboardKey),
            hashEntryKeys);

        var leaderboard = new TEntity[keysWithLeaderboardMetrics.Count];

        for (var i = 0; i < entityData.Length; i++)
        {
            var data = entityData[i];

            var entity = JsonSerializer.Deserialize<TEntity>(data);

            entity.Rank = keysWithLeaderboardMetrics[entity.Key].Rank;
            entity.Score = NormalizeScore(keysWithLeaderboardMetrics[entity.Key].Score);

            leaderboard[i] = entity;
        }

        return leaderboard;
    }

    private static (SortedSetEntry[] sortedSetEntries, HashEntry[] hashSetEntries, SortedSetEntry[] uniqueScoreEntries)
        PrepareEntitiesForLeaderboardAddOperation(IReadOnlyList<TEntity> entities)
    {
        const double initialScore = 0;
        
        var sortedSetEntries = new SortedSetEntry[entities.Count];
        var hashSetEntries = new HashEntry[entities.Count];

        var uniqueScoreEntries = new SortedSetEntry[]
        {
            new(initialScore, initialScore)
        };

        for (var i = 0; i < entities.Count; i++)
        {
            var entity = entities[i];
            
            sortedSetEntries[i] = new SortedSetEntry(entity.Key, initialScore);
            
            var serializedData = new RedisValue(JsonSerializer.Serialize(entity));

            hashSetEntries[i] = new HashEntry(entity.Key, serializedData);
        }
        
        return (sortedSetEntries, hashSetEntries, uniqueScoreEntries);
    }

    private static async Task TryExecuteTransactionAsync(
        ITransaction transaction,
        string errorMessage = "Failed to execute transaction!")
    {
        var commited = await transaction.ExecuteAsync();

        if (!commited)
        {
            throw new TransactionException(errorMessage);
        }
    }
    
    private static double NormalizeScore(double score) 
        => score < 0 ? -score : score;
}