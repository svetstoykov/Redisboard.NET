using Redisboard.NET.Common.Helpers;
using Redisboard.NET.Common.Models;
using Redisboard.NET.Helpers;
using StackExchange.Redis;

namespace Redisboard.NET.Benchmarks.Helpers;

internal static class BenchmarkLeaderboardHelper
{
    public static async Task InitializeBenchmarksLeaderboardAsync()
    {
        var connection = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
        var db = connection.GetDatabase(Constants.BenchmarkDbInstance);

        var leaderboard = new Leaderboard(db);

        if (await leaderboard.GetSizeAsync(Constants.LeaderboardKey) == default)
        {
            await LeaderboardSeeder.SeedAsync(
                leaderboard, Constants.LeaderboardKey, Constants.LeaderboardPlayerCount);
        }
    }
    
    public static async Task CleanUpBenchmarksLeaderboardAsync()
    {
        var connection = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
        var db = connection.GetDatabase(Constants.BenchmarkDbInstance);

        db.KeyDelete(CacheKey.ForEntityDataHashSet(Constants.LeaderboardKey));
        db.KeyDelete(CacheKey.ForUniqueScoreSortedSet(Constants.LeaderboardKey));
        db.KeyDelete(CacheKey.ForLeaderboardSortedSet(Constants.LeaderboardKey));
    }
}