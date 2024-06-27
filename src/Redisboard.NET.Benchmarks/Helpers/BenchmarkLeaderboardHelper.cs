using Redisboard.NET.Common.Helpers;
using Redisboard.NET.Common.Models;
using StackExchange.Redis;

namespace Redisboard.NET.Benchmarks.Helpers;

internal static class BenchmarkLeaderboardHelper
{
    public static async Task InitializeBenchmarksLeaderboardAsync()
    {
        var connection = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
        var db = connection.GetDatabase(Constants.BenchmarkDbInstance);

        var leaderboard = new Leaderboard<Player>(db);

        if (await leaderboard.GetSizeAsync(Constants.LeaderboardKey) < Constants.LeaderboardPlayerCount)
        {
            await LeaderboardSeeder.SeedAsync(
                leaderboard, Constants.LeaderboardKey, Constants.LeaderboardPlayerCount);
        }
    }
}