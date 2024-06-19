using System.Text.Json;
using Redisboard.NET.Enumerations;
using Redisboard.NET.Exceptions;
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
        if (entities == null)
        {
            throw new ArgumentNullException(nameof(entities), "The entities array cannot be null.");
        }

        var (sortedSetEntries, hashSetEntries, uniqueScoreEntries) =
            PrepareEntitiesForLeaderboardAddOperation(entities);

        var commandFlags = fireAndForget
            ? CommandFlags.FireAndForget
            : CommandFlags.None;

        await _redis.SortedSetAddAsync(
            CacheKey.ForLeaderboardSortedSet(leaderboardId),
            sortedSetEntries,
            commandFlags);

        await _redis.HashSetAsync(
            CacheKey.ForEntityDataHashSet(leaderboardId),
            hashSetEntries,
            commandFlags);

        await _redis.SortedSetAddAsync(
            CacheKey.ForUniqueScoreSortedSet(leaderboardId),
            uniqueScoreEntries,
            commandFlags);
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
                entityId,
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

    public async Task<TEntity> GetEntityDataAsync(object leaderboardId, string entityId)
    {
        var hashEntry = await _redis.HashGetAsync(
            CacheKey.ForEntityDataHashSet(leaderboardId),
            entityId);

        if (hashEntry == default)
        {
            // todo validation message
            throw new LeaderboardEntityNotFoundException();
        }

        return JsonSerializer.Deserialize<TEntity>(hashEntry);
    }

    public async Task<double> GetEntityScoreAsync(object leaderboardId, string entityId)
    {
        var score = await _redis.SortedSetScoreAsync(
            CacheKey.ForLeaderboardSortedSet(leaderboardId),
            entityId);

        if (score == null)
        {
            // todo validation message
            throw new LeaderboardEntityNotFoundException();
        }

        return score.Value;
    }

    public async Task<long> GetEntityRankAsync(
        object leaderboardId,
        string entityId,
        RankingType rankingType = RankingType.Default)
    {
        var playerIndex = await _redis
            .SortedSetRankAsync(
                CacheKey.ForLeaderboardSortedSet(leaderboardId),
                entityId,
                Order.Descending);

        if (playerIndex == null)
        {
            // todo validation message
            throw new LeaderboardEntityNotFoundException();
        }

        if (rankingType == RankingType.Default)
        {
            return playerIndex.Value + 1;
        }

        var entity = await GetLeaderboardAsync(
            leaderboardId, playerIndex.Value, 1, rankingType);

        return entity.Rank;
    }

    public async Task DeleteEntityAsync(object leaderboardId, string entityId)
    {
        await _redis.SortedSetRemoveAsync(
            CacheKey.ForLeaderboardSortedSet(leaderboardId),
            entityId);

        await _redis.HashDeleteAsync(
            CacheKey.ForEntityDataHashSet(leaderboardId),
            entityId);

        await _redis.SortedSetRemoveAsync(
            CacheKey.ForUniqueScoreSortedSet(leaderboardId),
            entityId);
    }

    public async Task<long> GetSizeAsync(object leaderboardId) 
        => await _redis.HashLengthAsync(CacheKey.ForEntityDataHashSet(leaderboardId));

    public async Task DeleteAsync(object leaderboardId)
    {
        RedisKey[] keys =
        [
            CacheKey.ForLeaderboardSortedSet(leaderboardId),
            CacheKey.ForEntityDataHashSet(leaderboardId),
            CacheKey.ForUniqueScoreSortedSet(leaderboardId)
        ];

        await _redis.KeyDeleteAsync(keys);
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
        Dictionary<string, long> entityIdsWithRank)
    {
        var hashEntryKeys = entityIdsWithRank
            .Select(p => new RedisValue(p.Key))
            .ToArray();

        var entityData = await _redis.HashGetAsync(
            CacheKey.ForEntityDataHashSet(leaderboardId),
            hashEntryKeys);

        var leaderboard = new TEntity[entityIdsWithRank.Count];

        for (var i = 0; i < entityData.Length; i++)
        {
            var data = entityData[i];

            var entity = JsonSerializer.Deserialize<TEntity>(data);

            entity.Rank = entityIdsWithRank[entity.Id];

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
}