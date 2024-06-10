using StackExchange.Redis;

namespace Redisboard.NET.Helpers;

internal static class CacheKey
{
    public static RedisKey ForEntityDataHashSet(object leaderboardId) 
        => new($"entity_data_hashset_leaderboard_{leaderboardId}");

    public static RedisKey ForLeaderboardSortedSet(object leaderboardId)
        => new($"sorted_set_leaderboard_{leaderboardId}");
    
    public static RedisKey ForUniqueScoreSortedSet(object leaderboardId)
        => new($"sorted_set_unique_score_{leaderboardId}");
}