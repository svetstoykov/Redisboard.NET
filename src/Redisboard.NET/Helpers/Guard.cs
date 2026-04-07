using Redisboard.NET.Interfaces;
using StackExchange.Redis;

namespace Redisboard.NET.Helpers;

internal static class Guard
{
    public static void AgainstNullOrEmptyCollection<T>(IEnumerable<T> values, string parameterName)
    {
        if (values is null)
            throw new ArgumentNullException(parameterName, "Collection cannot be null.");

        if (!values.Any())
            throw new ArgumentException("Collection cannot be empty.", parameterName);
    }

    public static void AgainstCollectionSizeExceeded(int count, int maxCount, string parameterName)
    {
        if (count > maxCount)
            throw new ArgumentOutOfRangeException(parameterName,
                $"Collection size cannot exceed {maxCount} items in a single operation.");
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

    public static void AgainstInvalidRankRange(long startRank, long endRank)
    {
        if (startRank < 1)
            throw new ArgumentOutOfRangeException(nameof(startRank), "Start rank must be >= 1.");

        if (endRank < startRank)
            throw new ArgumentOutOfRangeException(nameof(endRank), "End rank must be >= start rank.");
    }

    public static void AgainstInvalidMetadata(RedisValue metadata)
    {
        if (metadata.IsNull ||!metadata.HasValue || metadata == default)
            throw new ArgumentNullException(nameof(metadata), "Metadata cannot be null or empty.");
    }
}
