using System.Text.Json;
using Redisboard.NET.Enumerations;
using Redisboard.NET.Helpers;
using Redisboard.NET.Interfaces;
using StackExchange.Redis;

namespace Redisboard.NET.Services;

internal class Leaderboard<TEntity> : ILeaderboard<TEntity>
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
        object leaderboardId,
        TEntity[] entities,
        bool fireAndForget = false)
    {
        var (sortedSetEntries, hashSetEntries, uniqueScoreEntries) =
            PrepareEntitiesForLeaderboardAddOperation(entities);

        if (fireAndForget)
        {
            FireAndForgetAddToLeaderboard(
                leaderboardId, sortedSetEntries, hashSetEntries, uniqueScoreEntries);

            return;
        }

        await BatchAddToLeaderboardAsync(
            leaderboardId, sortedSetEntries, hashSetEntries, uniqueScoreEntries);
    }

    public async Task<TEntity[]> GetEntityAndNeighboursAsync(
        object leaderboardId,
        string entityId,
        int offset = 10,
        RankingType rankingType = RankingType.Default)
    {
        var playerIndex = await _redis
            .SortedSetRankAsync(
                CacheKey.ForLeaderboardSortedSet(leaderboardId),
                new RedisValue(entityId),
                Order.Descending);

        if (playerIndex == null)
        {
            return [];
        }

        var startIndex = Math.Max(playerIndex.Value - offset, 0);

        var pageSize = playerIndex.Value > offset
            ? offset * 2
            : (int)playerIndex.Value + offset;

        return await GetLeaderboardAsync(
            leaderboardId, startIndex, pageSize, rankingType);
    }

    public async Task<TEntity[]> GetEntitiesByScoreRangeAsync(
        object leaderboardId,
        double minScore,
        double maxScore,
        RankingType rankingType = RankingType.Default)
    {
        var entitiesInRange = await _redis
            .SortedSetRangeByScoreAsync(
                CacheKey.ForLeaderboardSortedSet(leaderboardId),
                minScore,
                maxScore,
                order: Order.Descending);

        if (!entitiesInRange.Any())
        {
            return [];
        }

        var startIndex = await _redis.SortedSetRankAsync(
            CacheKey.ForLeaderboardSortedSet(leaderboardId),
            entitiesInRange.First(), 
            Order.Descending);

        var pageSize = entitiesInRange.Length - 1;

        return await GetLeaderboardAsync(
            leaderboardId, startIndex!.Value, pageSize, rankingType);
    }

    public Task<TEntity> GetEntityDataAsync(object leaderboardId, string entityId)
    {
        throw new NotImplementedException();
    }

    public Task<double> GetEntityScoreAsync(object leaderboardId, string entityId)
    {
        throw new NotImplementedException();
    }

    public Task<long> GetEntityRankAsync(string entityId)
    {
        throw new NotImplementedException();
    }

    public Task DeleteEntityAsync(string entityId)
    {
        throw new NotImplementedException();
    }

    public Task<long> GetSizeAsync(object leaderboardId)
    {
        throw new NotImplementedException();
    }

    public Task ClearAsync(object leaderboardId)
    {
        throw new NotImplementedException();
    }

    public Task DeleteAsync(object leaderboardId)
    {
        throw new NotImplementedException();
    }

    private async Task<TEntity[]> GetLeaderboardAsync(
        object leaderboardId, long startIndex, int pageSize, RankingType rankingType)
    {
        var playerIdsWithRanking = rankingType switch
        {
            RankingType.Default
                => await GetPlayerIdsWithDefaultRanking(
                    leaderboardId, startIndex, pageSize),
            RankingType.DenseRank
                => await GetPlayerIdsWithDenseRanking(
                    leaderboardId, startIndex, pageSize),
            RankingType.ModifiedCompetition or RankingType.StandardCompetition
                => await GetPlayerIdsWithCompetitionRanking(
                    leaderboardId, startIndex, pageSize, (int)rankingType),
            _ => throw new KeyNotFoundException(
                $"Ranking type not found! Valid ranking types are: " +
                $"{string.Join(", ", Enum.GetValues(typeof(RankingType)).Cast<RankingType>().Select(type => $"{type} ({(int)type})"))}.")
        };

        return await GetEntitiesDataAsync(leaderboardId, playerIdsWithRanking);
    }

    private async Task<Dictionary<string, long>> GetPlayerIdsWithDefaultRanking(
        object leaderboardId, long startIndex, int pageSize)
    {
        var endIndex = startIndex + pageSize;
        
        var playerIdsWithRanking = new Dictionary<string, long>();

        var entities = await _redis.SortedSetRangeByRankAsync(
            CacheKey.ForLeaderboardSortedSet(leaderboardId),
            startIndex,
            endIndex,
            Order.Descending);

        var rankForTopPlayer = startIndex + 1;
        for (var i = 0; i < entities.Length; i++)
        {
            playerIdsWithRanking.Add(
                entities[i], rankForTopPlayer + i);
        }

        return playerIdsWithRanking;
    }

    private async Task<Dictionary<string, long>> GetPlayerIdsWithDenseRanking(
        object leaderboardId, long startIndex, int pageSize)
    {
        var script = LuaScript
            .Prepare(LeaderboardScript.ForPlayerIdsByRangeWithDenseRank())
            .ExecutableScript;

        RedisKey[] keys =
        [
            CacheKey.ForLeaderboardSortedSet(leaderboardId),
            CacheKey.ForUniqueScoreSortedSet(leaderboardId)
        ];

        RedisValue[] args = [startIndex, pageSize];

        return await EvaluateScriptToDictionaryAsync(script, keys, args);
    }

    private async Task<Dictionary<string, long>> GetPlayerIdsWithCompetitionRanking(
        object leaderboardId, long startIndex, int pageSize, int rankingType)
    {
        var script = LuaScript
            .Prepare(LeaderboardScript.ForPlayerIdsByRangeWithCompetitionRank())
            .ExecutableScript;

        RedisKey[] keys =
        [
            CacheKey.ForLeaderboardSortedSet(leaderboardId)
        ];

        RedisValue[] args = [startIndex, pageSize, rankingType];

        return await EvaluateScriptToDictionaryAsync(script, keys, args);
    }

    private async Task<Dictionary<string, long>> EvaluateScriptToDictionaryAsync(
        string script, RedisKey[] keys, RedisValue[] args)
    {
        var results = await _redis.ScriptEvaluateReadOnlyAsync(script, keys.ToArray(), args.ToArray());

        return ((RedisResult[])results ?? [])
            .Select(r => (string[])r)
            .ToDictionary(r => r[0], r => long.Parse(r[1]));
    }

    private async Task<TEntity[]> GetEntitiesDataAsync(
        object leaderboardId,
        Dictionary<string, long> playerIdsWithRank)
    {
        var batch = _redis.CreateBatch();

        var dataRetrievalTasks = playerIdsWithRank
            .Select(p => _redis.HashGetAsync(CacheKey.ForEntityDataHashSet(leaderboardId), p.Key))
            .ToList();

        batch.Execute();

        await Task.WhenAll(dataRetrievalTasks);

        var leaderboard = new TEntity[playerIdsWithRank.Count];

        for (var i = 0; i < dataRetrievalTasks.Count; i++)
        {
            var result = dataRetrievalTasks[i].Result;

            var entity = JsonSerializer.Deserialize<TEntity>(result);

            entity.Rank = playerIdsWithRank[entity.Id];

            leaderboard[i] = entity;
        }

        return leaderboard;
    }

    private static (SortedSetEntry[] sortedSetEntries, HashEntry[] hashSetEntries, SortedSetEntry[] uniqueScoreEntries)
        PrepareEntitiesForLeaderboardAddOperation(IReadOnlyList<TEntity> entities)
    {
        var sortedSetEntries = new SortedSetEntry[entities.Count];
        var hashSetEntries = new HashEntry[entities.Count];
        var uniqueScores = new HashSet<double>();

        for (var i = 0; i < entities.Count; i++)
        {
            var entity = entities[i];

            var redisValueId = new RedisValue(entity.Id);

            sortedSetEntries[i] = new SortedSetEntry(redisValueId, entity.Score);

            uniqueScores.Add(entity.Score);

            var serializedData = new RedisValue(JsonSerializer.Serialize(entity));

            hashSetEntries[i] = new HashEntry(redisValueId, serializedData);
        }

        var uniqueScoreEntries = uniqueScores
            .Select(score => new SortedSetEntry(score, score)).ToArray();

        return (sortedSetEntries, hashSetEntries, uniqueScoreEntries);
    }

    private void FireAndForgetAddToLeaderboard(
        object leaderboardId,
        SortedSetEntry[] sortedSetEntries,
        HashEntry[] hashSetEntries,
        SortedSetEntry[] uniqueScoreSortedSetEntries)
    {
        _redis.SortedSetAdd(
            CacheKey.ForLeaderboardSortedSet(leaderboardId), sortedSetEntries, CommandFlags.FireAndForget);

        _redis.HashSet(
            CacheKey.ForEntityDataHashSet(leaderboardId), hashSetEntries, CommandFlags.FireAndForget);

        _redis.SortedSetAdd(
            CacheKey.ForUniqueScoreSortedSet(leaderboardId), uniqueScoreSortedSetEntries, CommandFlags.FireAndForget);

        _redis.Ping();
    }

    private async Task BatchAddToLeaderboardAsync(
        object leaderboardId,
        SortedSetEntry[] sortedSetEntries,
        HashEntry[] hashSetEntries,
        SortedSetEntry[] uniqueScoreEntries)
    {
        var batch = _redis.CreateBatch();

        var addToSortedSetTask = _redis.SortedSetAddAsync(
            CacheKey.ForLeaderboardSortedSet(leaderboardId), sortedSetEntries);

        var addToHashSetTask = _redis.HashSetAsync(
            CacheKey.ForEntityDataHashSet(leaderboardId), hashSetEntries);

        var addToUniqueScoreSortedSetTask = _redis.SortedSetAddAsync(
            CacheKey.ForUniqueScoreSortedSet(leaderboardId), uniqueScoreEntries);

        batch.Execute();

        await Task.WhenAll(addToSortedSetTask, addToHashSetTask, addToUniqueScoreSortedSetTask);
    }
}