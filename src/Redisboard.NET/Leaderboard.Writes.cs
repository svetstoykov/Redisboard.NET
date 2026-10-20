#nullable enable

using System.Globalization;
using Redisboard.NET.Enumerations;
using Redisboard.NET.Helpers;
using Redisboard.NET.Interfaces;
using Redisboard.NET.Models;
using StackExchange.Redis;

namespace Redisboard.NET;

public partial class Leaderboard<TEntity> where TEntity : ILeaderboardEntity, new()
{
    /// <inheritdoc/>
    public Task<ScoreUpdateResult> IncrementEntityScoreAsync(
        RedisValue leaderboardKey, RedisValue entityKey, double delta,
        RankingType rankingType = RankingType.Default, CancellationToken cancellationToken = default)
    {
        if (double.IsNaN(delta) || double.IsInfinity(delta))
            throw new ArgumentOutOfRangeException(nameof(delta), "Delta must be finite.");
        return MutateScoreAsync(leaderboardKey, entityKey, "increment", delta,
            ScoreUpdateCondition.Exists, rankingType, null, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ScoreUpdateResult> DecrementEntityScoreAsync(
        RedisValue leaderboardKey, RedisValue entityKey, double amount,
        RankingType rankingType = RankingType.Default, CancellationToken cancellationToken = default)
    {
        Guard.AgainstInvalidScore(amount);
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be non-negative.");
        return IncrementEntityScoreAsync(leaderboardKey, entityKey, -amount, rankingType, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ScoreUpdateResult> UpdateEntityScoreConditionalAsync(
        RedisValue leaderboardKey, RedisValue entityKey, double score, ScoreUpdateCondition condition,
        RankingType rankingType = RankingType.Default, CancellationToken cancellationToken = default)
    {
        Guard.AgainstInvalidScore(score);
        return MutateScoreAsync(leaderboardKey, entityKey, "set", score,
            condition, rankingType, null, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ScoreUpdateResult> AddEntityConditionalAsync(
        RedisValue leaderboardKey, TEntity entity, ScoreUpdateCondition condition,
        RankingType rankingType = RankingType.Default, CancellationToken cancellationToken = default)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);
        if (entity is null) throw new ArgumentNullException(nameof(entity));
        var entityKey = EntityTypeAccessor<TEntity>.GetKey(entity);
        Guard.AgainstInvalidIdentityKey(entityKey);
        var score = EntityTypeAccessor<TEntity>.GetScore(entity);
        Guard.AgainstInvalidScore(score);
        ValidateScoreMutation(condition, rankingType);
        cancellationToken.ThrowIfCancellationRequested();
        return MutateScoreAsync(leaderboardKey, entityKey, "add", score,
            condition, rankingType, this._serializer.Serialize(entity), cancellationToken);
    }

    private async Task<ScoreUpdateResult> MutateScoreAsync(
        RedisValue leaderboardKey, RedisValue entityKey, string operation, double value,
        ScoreUpdateCondition condition, RankingType rankingType, byte[]? metadata,
        CancellationToken cancellationToken)
    {
        Guard.AgainstInvalidIdentityKey(leaderboardKey);
        Guard.AgainstInvalidIdentityKey(entityKey);
        ValidateScoreMutation(condition, rankingType);
        cancellationToken.ThrowIfCancellationRequested();
        RedisKey[] keys =
        [
            CacheKey.ForLeaderboardSortedSet(leaderboardKey, this._options),
            CacheKey.ForUniqueScoreSortedSet(leaderboardKey, this._options),
            CacheKey.ForEntityDataHashSet(leaderboardKey, this._options)
        ];
        RedisValue[] args = [entityKey, operation, value, (int)condition, (int)rankingType, metadata ?? [], this._scoreOrder == ScoreOrder.Descending ? -1 : 1];
        var result = ((RedisResult[]?)await this._redis.ScriptEvaluateAsync(
            LeaderboardScript.ForMutateEntityScore().ExecutableScript, keys, args))!;
        var applied = (long)result[0];
        if (applied == -1)
            throw new ArgumentOutOfRangeException(nameof(value), "Resulting score must be finite.");
        if (result[1].IsNull)
            return new ScoreUpdateResult(false, null, null);
        var score = double.Parse(((string?)result[1])!, CultureInfo.InvariantCulture);
        return new ScoreUpdateResult(applied == 1, NormalizeScore(score), (long)result[2]);
    }

    private static void ValidateScoreMutation(ScoreUpdateCondition condition, RankingType rankingType)
    {
        if (condition is < ScoreUpdateCondition.Always or > ScoreUpdateCondition.Lower)
            throw new ArgumentOutOfRangeException(nameof(condition), "Unknown score update condition.");
        ValidateRankingType(rankingType);
    }

}
