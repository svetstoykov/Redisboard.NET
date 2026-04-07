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
    /// <returns>A Redis key for the entity data hash set.</returns>
    public static RedisKey ForEntityDataHashSet(RedisValue leaderboardKey) 
        => new($"entity_data_hashset_leaderboard_{leaderboardKey}");

    /// <summary>
    /// Generates the sorted set key for the leaderboard rankings.
    /// </summary>
    /// <param name="leaderboardKey">The leaderboard identifier.</param>
    /// <returns>A Redis key for the leaderboard sorted set.</returns>
    public static RedisKey ForLeaderboardSortedSet(RedisValue leaderboardKey)
        => new($"sorted_set_leaderboard_{leaderboardKey}");
    
    /// <summary>
    /// Generates the sorted set key for unique scores tracking.
    /// </summary>
    /// <param name="leaderboardKey">The leaderboard identifier.</param>
    /// <returns>A Redis key for the unique scores sorted set.</returns>
    public static RedisKey ForUniqueScoreSortedSet(RedisValue leaderboardKey)
        => new($"sorted_set_unique_score_{leaderboardKey}");
}