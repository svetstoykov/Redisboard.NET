using Redisboard.NET.Common.Models;
using Redisboard.NET.Interfaces;
using StackExchange.Redis;

namespace Redisboard.NET.Common.Helpers;

public static class LeaderboardSeeder
{
    public static async Task SeedAsync(
        ILeaderboard<Player> leaderboard,
        RedisValue leaderboardId,
        int playersCount)
    {
        const int batchSize = 10_000;

        var batchesCount = (int)Math.Ceiling((double)playersCount / batchSize);

        for (var i = 0; i < batchesCount; i++)
        {
            var currentBatchSize = Math.Min(batchSize, playersCount - (i * batchSize));
            var players = new Player[currentBatchSize];

            for (var j = 0; j < currentBatchSize; j++)
            {
                players[j] = Player.New();
            }

            await leaderboard.AddEntitiesAsync(leaderboardId, players, fireAndForget: true);
        }
    }
}
