using System.Text.Json;
using Redisboard.NET.Enumerations;
using Redisboard.NET.Helpers;
using Redisboard.NET.Interfaces;
using StackExchange.Redis;

namespace Redisboard.NET.Services;

public class LeaderboardManager<TEntity> : ILeaderboardManager<TEntity>
    where TEntity : ILeaderboardEntity
{
    private readonly IDatabase _redis;

    public LeaderboardManager(IConnectionMultiplexer connectionMultiplexer)
    {
        _redis = connectionMultiplexer.GetDatabase();
    }

    public async Task AddToLeaderboardAsync(
        object leaderboardId,
        TEntity[] entities,
        CancellationToken cancellationToken = default)
    {
        var (sortedSetEntries, hashSetEntries, uniqueScoreEntries) = PrepareEntitiesForRedisAddOperation(entities);

        var redisOperations = new List<Task>();
        
        var batch = _redis.CreateBatch();
        
        redisOperations.Add(_redis.SortedSetAddAsync(
            CacheKey.ForLeaderboardSortedSet(leaderboardId), sortedSetEntries));

        redisOperations.Add(_redis.HashSetAsync(
            CacheKey.ForEntityDataHashSet(leaderboardId), hashSetEntries));

        redisOperations.Add(_redis.SortedSetAddAsync(
            CacheKey.ForUniqueScoreSortedSet(leaderboardId), uniqueScoreEntries));
        
        batch.Execute();

        await Task.WhenAll(redisOperations);
    }

    public void AddToLeaderboard(object leaderboardId, TEntity[] entities)
    {
        var (sortedSetEntries, hashSetEntries, uniqueScoreEntries) = PrepareEntitiesForRedisAddOperation(entities);

        var isServerLive = _redis.Ping();
        
        _redis.SortedSetAdd(
            CacheKey.ForLeaderboardSortedSet(leaderboardId), sortedSetEntries, CommandFlags.FireAndForget);
        
        _redis.HashSetAsync(
            CacheKey.ForEntityDataHashSet(leaderboardId), hashSetEntries, CommandFlags.FireAndForget);

        _redis.SortedSetAddAsync(
            CacheKey.ForUniqueScoreSortedSet(leaderboardId), uniqueScoreEntries, CommandFlags.FireAndForget);
    }

    public async Task<TEntity> GetLeaderboardEntityByIdAsync(
        object leaderboardId,
        string id,
        RankingType rankingType,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public async Task<TEntity[]> GetPaginatedLeaderboardAsync(
        object leaderboardId,
        int pageIndex,
        int pageSize,
        RankingType rankingType = default,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public async Task<TEntity[]> GetLeaderboardByScoreRangeAsync(
        object leaderboardId,
        double minScoreValue,
        double maxScoreValue,
        RankingType rankingType = default,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    private static (SortedSetEntry[] sortedSetEntries, HashEntry[] hashSetEntries, SortedSetEntry[] uniqueScoreEntries)
        PrepareEntitiesForRedisAddOperation(IReadOnlyList<TEntity> entities)
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
}