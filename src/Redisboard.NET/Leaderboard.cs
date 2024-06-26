using System.Text.Json;
using System.Transactions;
using Redisboard.NET.Enumerations;
using Redisboard.NET.Helpers;
using Redisboard.NET.Interfaces;
using StackExchange.Redis;

namespace Redisboard.NET;

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
        object leaderboardKey,
        TEntity[] entities,
        bool fireAndForget = false)
    {
        Guard.AgainstInvalidLeaderboardKey(leaderboardKey);
        Guard.AgainstInvalidLeaderboardEntities(entities);

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

    public async Task<TEntity[]> GetEntityAndNeighboursAsync(
        object leaderboardKey,
        string entityKey,
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
        object leaderboardKey,
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

    public async Task<TEntity> GetEntityDataAsync(object leaderboardKey, string entityKey)
    {
        Guard.AgainstInvalidLeaderboardKey(leaderboardKey);
        Guard.AgainstInvalidEntityKey(entityKey);

        var hashEntry = await _redis.HashGetAsync(
            CacheKey.ForEntityDataHashSet(leaderboardKey),
            entityKey);

        return hashEntry == default
            ? default
            : JsonSerializer.Deserialize<TEntity>(hashEntry);
    }

    public async Task<double?> GetEntityScoreAsync(object leaderboardKey, string entityKey)
    {
        Guard.AgainstInvalidLeaderboardKey(leaderboardKey);
        Guard.AgainstInvalidEntityKey(entityKey);

        var score = await _redis.SortedSetScoreAsync(
            CacheKey.ForLeaderboardSortedSet(leaderboardKey),
            entityKey);

        return score;
    }

    public async Task<long?> GetEntityRankAsync(
        object leaderboardKey,
        string entityKey,
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

    public async Task DeleteEntityAsync(object leaderboardKey, string entityKey)
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

    public async Task<long> GetSizeAsync(object leaderboardKey)
    {
        Guard.AgainstInvalidLeaderboardKey(leaderboardKey);

        return await _redis.HashLengthAsync(CacheKey.ForEntityDataHashSet(leaderboardKey));
    }

    public async Task DeleteAsync(object leaderboardKey)
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
        object leaderboardKey, long startIndex, int pageSize, RankingType rankingType)
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

    private async Task<Dictionary<string, long>> GetEntityKeysWithDefaultRanking(
        object leaderboardKey, long startIndex, int pageSize)
    {
        var endIndex = startIndex + pageSize;

        var entityKeysWithRanking = new Dictionary<string, long>();

        var entities = await _redis.SortedSetRangeByRankAsync(
            CacheKey.ForLeaderboardSortedSet(leaderboardKey),
            startIndex,
            endIndex);

        var rankForTopPlayer = startIndex + 1;
        for (var i = 0; i < entities.Length; i++)
        {
            entityKeysWithRanking.Add(
                entities[i], rankForTopPlayer + i);
        }

        return entityKeysWithRanking;
    }

    private async Task<Dictionary<string, long>> GetEntityKeysWithDenseRanking(
        object leaderboardKey, long startIndex, int pageSize)
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

    private async Task<Dictionary<string, long>> GetEntityKeysWithCompetitionRanking(
        object leaderboardKey, long startIndex, int pageSize, int rankingType)
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

    private async Task<Dictionary<string, long>> EvaluateScriptToDictionaryAsync(
        string script, RedisKey[] keys, RedisValue[] args)
    {
        var results = await _redis.ScriptEvaluateReadOnlyAsync(script, keys.ToArray(), args.ToArray());

        return ((RedisResult[])results ?? [])
            .Select(r => (string[])r)
            .ToDictionary(r => r[0], r => long.Parse(r[1]));
    }

    private async Task<TEntity[]> GetEntitiesDataAsync(
        object leaderboardKey,
        Dictionary<string, long> entityKeysWithRank)
    {
        var hashEntryKeys = entityKeysWithRank
            .Select(p => new RedisValue(p.Key))
            .ToArray();

        var entityData = await _redis.HashGetAsync(
            CacheKey.ForEntityDataHashSet(leaderboardKey),
            hashEntryKeys);

        var leaderboard = new TEntity[entityKeysWithRank.Count];

        for (var i = 0; i < entityData.Length; i++)
        {
            var data = entityData[i];

            var entity = JsonSerializer.Deserialize<TEntity>(data);

            entity.Rank = entityKeysWithRank[entity.Key];
            entity.Score *= -1;

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

            // By default, Redis ranks things from lowest to highest score.
            // For leaderboards, we want the top scorers at the beginning (highest to lowest).
            // But if we request Order.Descending when querying we will also reverse the lexicographical order (for entries with same score)
            // To solve this, we simply store the scores as negative numbers.
            // This way, when we ask for the regular order (lowest to highest),
            // it actually gives us the highest scores (because they're negative) first.
            var invertedScore = -entity.Score;

            var redisValueId = new RedisValue(entity.Key);

            sortedSetEntries[i] = new SortedSetEntry(redisValueId, invertedScore);

            uniqueScores.Add(invertedScore);

            var serializedData = new RedisValue(JsonSerializer.Serialize(entity));

            hashSetEntries[i] = new HashEntry(redisValueId, serializedData);
        }

        var uniqueScoreEntries = uniqueScores
            .Select(score => new SortedSetEntry(score, score)).ToArray();

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
}