using Redisboard.NET.Interfaces;
using StackExchange.Redis;

namespace Redisboard.NET.Helpers;

/// <summary>
/// Provides validation guard methods for leaderboard operations.
/// </summary>
internal static class Guard
{
    /// <summary>
    /// Ensures a collection is not null or empty.
    /// </summary>
    /// <typeparam name="T">The collection element type.</typeparam>
    /// <param name="values">The collection to validate.</param>
    /// <param name="parameterName">Name of the parameter for exception messages.</param>
    /// <exception cref="ArgumentNullException">Thrown when the collection is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the collection is empty.</exception>
    public static void AgainstNullOrEmptyCollection<T>(IEnumerable<T> values, string parameterName)
    {
        if (values is null)
            throw new ArgumentNullException(parameterName, "Collection cannot be null.");

        if (!values.Any())
            throw new ArgumentException("Collection cannot be empty.", parameterName);
    }

    /// <summary>
    /// Ensures a collection size does not exceed the maximum allowed.
    /// </summary>
    /// <param name="count">The actual collection count.</param>
    /// <param name="maxCount">The maximum allowed count.</param>
    /// <param name="parameterName">Name of the parameter for exception messages.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when count exceeds maxCount.</exception>
    public static void AgainstCollectionSizeExceeded(int count, int maxCount, string parameterName)
    {
        if (count > maxCount)
            throw new ArgumentOutOfRangeException(parameterName,
                $"Collection size cannot exceed {maxCount} items in a single operation.");
    }

    /// <summary>
    /// Ensures an entity key is valid (not null, empty, or whitespace).
    /// </summary>
    /// <param name="identityKey">The entity key to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown when the key is null or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a numeric key is not positive.</exception>
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

    /// <summary>
    /// Ensures an offset is not negative.
    /// </summary>
    /// <param name="offset">The offset to validate.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when offset is negative.</exception>
    public static void AgainstInvalidOffset(int offset)
    {
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be negative!");
    }

    /// <summary>
    /// Ensures a score range limit is not negative.
    /// </summary>
    /// <param name="limit">The score range limit to validate.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when limit is negative.</exception>
    public static void AgainstInvalidScoreRangeLimit(double limit)
    {
        if (limit < 0)
            throw new ArgumentOutOfRangeException(nameof(limit), "Score range limit cannot be negative!");
    }

    /// <summary>
    /// Ensures min score is not greater than max score.
    /// </summary>
    /// <param name="minScore">The minimum score.</param>
    /// <param name="maxScore">The maximum score.</param>
    /// <exception cref="InvalidOperationException">Thrown when minScore exceeds maxScore.</exception>
    public static void AgainstInvalidScoreRange(double minScore, double maxScore)
    {
        if (minScore > maxScore)
            throw new InvalidOperationException("Min score cannot be greater than max score!");
    }

    /// <summary>
    /// Ensures a score is not negative.
    /// </summary>
    /// <param name="newScore">The score to validate.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when score is negative.</exception>
    public static void AgainstInvalidScore(double newScore)
    {
        if (newScore < 0)
            throw new ArgumentOutOfRangeException(nameof(newScore), "Score cannot be negative!");
    }

    /// <summary>
    /// Ensures rank range is valid (start >= 1 and end >= start).
    /// </summary>
    /// <param name="startRank">The starting rank (1-based).</param>
    /// <param name="endRank">The ending rank (1-based).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when ranks are invalid.</exception>
    public static void AgainstInvalidRankRange(long startRank, long endRank)
    {
        if (startRank < 1)
            throw new ArgumentOutOfRangeException(nameof(startRank), "Start rank must be >= 1.");

        if (endRank < startRank)
            throw new ArgumentOutOfRangeException(nameof(endRank), "End rank must be >= start rank.");
    }

    /// <summary>
    /// Ensures metadata is not null or empty.
    /// </summary>
    /// <param name="metadata">The metadata to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown when metadata is null or empty.</exception>
    public static void AgainstInvalidMetadata(RedisValue metadata)
    {
        if (metadata.IsNull ||!metadata.HasValue || metadata == default)
            throw new ArgumentNullException(nameof(metadata), "Metadata cannot be null or empty.");
    }
}
