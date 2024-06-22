using StackExchange.Redis;

namespace Redisboard.NET.Helpers;

internal static class CacheKey
{
    public static RedisKey ForEntityDataHashSet(object leaderboardKey) 
        => new($"entity_data_hashset_leaderboard_{leaderboardKey}");

    public static RedisKey ForLeaderboardSortedSet(object leaderboardKey)
        => new($"sorted_set_leaderboard_{leaderboardKey}");
    
    public static RedisKey ForUniqueScoreSortedSet(object leaderboardKey)
        => new($"sorted_set_unique_score_{leaderboardKey}");
}