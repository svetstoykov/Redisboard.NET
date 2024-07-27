using Redisboard.NET.Interfaces;
using StackExchange.Redis;

namespace Redisboard.NET.Helpers;

internal static class Guard
{
    public static void AgainstInvalidLeaderboardEntities(ILeaderboardEntity entity)
    {
        if (entity == null )
            throw new ArgumentNullException(nameof(entity), "The leaderboard entity cannot be null.");
    }

    public static void AgainstInvalidIdentityKey(RedisValue identityKey)
    {
        if (identityKey.IsNull || !identityKey.HasValue || identityKey == default)
            throw new ArgumentNullException(nameof(identityKey), "Entity key cannot be null or empty.");

        if (identityKey.IsInteger)
        {
            var longValue = (long)identityKey;
            if (longValue <= default(long))
                throw new ArgumentOutOfRangeException(nameof(identityKey), "Numeric entity key must be positive.");
        }

        var stringValue = identityKey.ToString();
        if (string.IsNullOrEmpty(stringValue))
            throw new ArgumentException("Entity key cannot be empty or whitespace.", nameof(identityKey));
    }

    public static void AgainstInvalidOffset(int offset)
    {
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be negative!");
    }

    public static void AgainstInvalidScoreRangeLimit(double limit)
    {
        if (limit < 0)
            throw new ArgumentOutOfRangeException(nameof(limit), "Score range limit cannot be negative!");
    }

    public static void AgainstInvalidScoreRange(double minScore, double maxScore)
    {
        if (minScore > maxScore)
            throw new InvalidOperationException("Min score cannot be greater than max score!");
    }

    public static void AgainstInvalidScore(double newScore)
    {
        if (newScore < 0)
            throw new ArgumentOutOfRangeException(nameof(newScore), "Score cannot be negative!");
    }
}