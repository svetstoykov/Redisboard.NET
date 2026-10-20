#nullable enable
using Redisboard.NET.Enumerations;
using Redisboard.NET.Helpers;
using Redisboard.NET.Interfaces;
using Redisboard.NET.Models;
using StackExchange.Redis;

namespace Redisboard.NET;

public partial class Leaderboard<TEntity>
    where TEntity : ILeaderboardEntity, new()
{
    /// <inheritdoc />
    public async Task<TEntity?> GetEntityAsync(RedisValue leaderboardKey, RedisValue entityKey,
        RankingType rankingType = RankingType.Default, CancellationToken cancellationToken = default)
    {
        var entities = await this.GetEntitiesByKeysAsync(leaderboardKey, [entityKey], rankingType, cancellationToken);
        return entities.Length == 0 ? default : entities[0];
    }

    /// <inheritdoc />
    public async Task<TEntity[]> GetEntitiesByKeysAsync(RedisValue leaderboardKey, IEnumerable<RedisValue> entityKeys,
        RankingType rankingType = RankingType.Default, CancellationToken cancellationToken = default)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);
        if (entityKeys is null) throw new ArgumentNullException(nameof(entityKeys));
        var members = entityKeys.Take(DefaultMaxBatchOperationSize + 1).ToArray();
        Guard.AgainstNullOrEmptyCollection(members, nameof(entityKeys));
        Guard.AgainstCollectionSizeExceeded(members.Length, DefaultMaxBatchOperationSize, nameof(entityKeys));
        foreach (var member in members) Guard.AgainstInvalidIdentityKey(member);
        ValidateRankingType(rankingType);
        cancellationToken.ThrowIfCancellationRequested();
        var args = new RedisValue[members.Length + 2];
        args[0] = "keys";
        args[1] = (int)rankingType;
        Array.Copy(members, 0, args, 2, members.Length);
        var result = await this._redis.ScriptEvaluateReadOnlyAsync(LeaderboardScript.ForEntities().ExecutableScript,
            this.GetReadKeys(leaderboardKey), args);
        cancellationToken.ThrowIfCancellationRequested();
        return this.MapEntityRows(result, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<LeaderboardPage<TEntity>> GetPageAsync(RedisValue leaderboardKey, int pageNumber, int pageSize,
        RankingType rankingType = RankingType.Default, CancellationToken cancellationToken = default)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);
        if (pageNumber < 1) throw new ArgumentOutOfRangeException(nameof(pageNumber));
        if (pageSize < 1 || pageSize > DefaultMaxBatchOperationSize) throw new ArgumentOutOfRangeException(nameof(pageSize));
        ValidateRankingType(rankingType);
        cancellationToken.ThrowIfCancellationRequested();
        var start = ((long)pageNumber - 1) * pageSize;
        RedisValue[] args = ["page", (int)rankingType, start, start + pageSize - 1];
        var result = await this._redis.ScriptEvaluateReadOnlyAsync(LeaderboardScript.ForEntities().ExecutableScript,
            this.GetReadKeys(leaderboardKey), args);
        cancellationToken.ThrowIfCancellationRequested();
        var response = (RedisResult[])result!;
        return new LeaderboardPage<TEntity>(this.MapEntityRows(response[0], cancellationToken), (long)response[1]);
    }

    /// <inheritdoc />
    public async Task<bool> ExpireAsync(RedisValue leaderboardKey, TimeSpan? expiry,
        CancellationToken cancellationToken = default)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);
        if (expiry < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(expiry));
        cancellationToken.ThrowIfCancellationRequested();
        RedisValue milliseconds = expiry.HasValue ? (RedisValue)(long)Math.Ceiling(expiry.Value.TotalMilliseconds) : "persist";
        var result = await this._redis.ScriptEvaluateAsync(LeaderboardScript.ForExpire().ExecutableScript,
            this.GetReadKeys(leaderboardKey), [milliseconds]);
        return (long)result != 0;
    }

    private RedisKey[] GetReadKeys(RedisValue leaderboardKey) =>
    [
        CacheKey.ForLeaderboardSortedSet(leaderboardKey),
        CacheKey.ForUniqueScoreSortedSet(leaderboardKey),
        CacheKey.ForEntityDataHashSet(leaderboardKey)
    ];
}
