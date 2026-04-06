using Redisboard.NET.Common.Helpers;
using Redisboard.NET.Common.Models;
using Redisboard.NET.Helpers;
using Redisboard.NET.Serialization;
using StackExchange.Redis;

namespace Redisboard.NET.Benchmarks.Helpers;

internal static class BenchmarkLeaderboardHelper
{
    public static async Task InitializeBenchmarksLeaderboardAsync()
    {
        var connection = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
        var db = connection.GetDatabase(Settings.BenchmarkDbInstance);

        var leaderboard = new Leaderboard<Player>(db, new MemoryPackLeaderboardSerializer());

        if (await leaderboard.GetSizeAsync(Settings.LeaderboardKey()) == default)
        {
            await LeaderboardSeeder.SeedAsync(
                leaderboard, Settings.LeaderboardKey(), Settings.LeaderboardPlayerCount);
        }
    }

    public static async Task CleanUpBenchmarksLeaderboardAsync()
    {
        var connection = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
        var db = connection.GetDatabase(Settings.BenchmarkDbInstance);

        db.KeyDelete(CacheKey.ForEntityDataHashSet(Settings.LeaderboardKey()));
        db.KeyDelete(CacheKey.ForUniqueScoreSortedSet(Settings.LeaderboardKey()));
        db.KeyDelete(CacheKey.ForLeaderboardSortedSet(Settings.LeaderboardKey()));
    }
}
