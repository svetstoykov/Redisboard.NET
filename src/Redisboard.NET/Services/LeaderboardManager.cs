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
        bool fireAndForget = false,
        CancellationToken cancellationToken = default)
    {
        var (sortedSetEntries, hashSetEntries, uniqueScoreEntries) = PrepareEntitiesForRedisAddOperation(entities);
        
        if (fireAndForget)
        {
            FireAndForgetAddToLeaderboard(
                leaderboardId, sortedSetEntries, hashSetEntries, uniqueScoreEntries);
            
            return;
        }

        await BatchAddToLeaderboardAsync(
            leaderboardId, sortedSetEntries, hashSetEntries, uniqueScoreEntries);
    }

    public async Task<TEntity[]> GetEntityAndNeighboursByIdAsync(
        object leaderboardId,
        string id,
        int offset = 10,
        RankingType rankingType = default,
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