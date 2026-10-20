using System.Security.Cryptography;
using StackExchange.Redis;

namespace Redisboard.NET.Helpers;

/// <summary>
/// Generates Redis cache keys for leaderboard data structures.
/// </summary>
internal static class CacheKey
{
    /// <summary>
    /// Generates the hash key for storing entity metadata.
    /// </summary>
    /// <param name="leaderboardKey">The leaderboard identifier.</param>
    /// <param name="options">Optional key namespace and cluster hash tag settings.</param>
    /// <returns>A Redis key for the entity data hash set.</returns>
    public static RedisKey ForEntityDataHashSet(RedisValue leaderboardKey, LeaderboardOptions options = null)
        => Create("entity_data_hashset_leaderboard_", leaderboardKey, options);

    /// <summary>
    /// Generates the sorted set key for the leaderboard rankings.
    /// </summary>
    /// <param name="leaderboardKey">The leaderboard identifier.</param>
    /// <param name="options">Optional key namespace and cluster hash tag settings.</param>
    /// <returns>A Redis key for the leaderboard sorted set.</returns>
    public static RedisKey ForLeaderboardSortedSet(RedisValue leaderboardKey, LeaderboardOptions options = null)
        => Create("sorted_set_leaderboard_", leaderboardKey, options);

    /// <summary>
    /// Generates the sorted set key for unique scores tracking.
    /// </summary>
    /// <param name="leaderboardKey">The leaderboard identifier.</param>
    /// <param name="options">Optional key namespace and cluster hash tag settings.</param>
    /// <returns>A Redis key for the unique scores sorted set.</returns>
    public static RedisKey ForUniqueScoreSortedSet(RedisValue leaderboardKey, LeaderboardOptions options = null)
        => Create("sorted_set_unique_score_", leaderboardKey, options);
    private static RedisKey Create(string structure, RedisValue leaderboardKey, LeaderboardOptions options)
    {
        var prefix = options?.KeyPrefix ?? string.Empty;
        if (options?.UseClusterHashTags != true)
            return new RedisKey($"{prefix}{structure}{leaderboardKey}");

        using var hash = SHA256.Create();
        var digest = BitConverter.ToString(hash.ComputeHash((byte[])leaderboardKey)).Replace("-", string.Empty);
        return new RedisKey($"{prefix}{structure}{{{digest}}}:{leaderboardKey}");
    }
}
