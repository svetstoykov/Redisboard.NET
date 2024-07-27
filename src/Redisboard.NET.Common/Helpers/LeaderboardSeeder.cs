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
        const int batchInsertRepeat = 100;

        var playersPerBatch = playersCount / 100;

        for (var i = 0; i < batchInsertRepeat; i++)
        {
            for (var j = 0; j < playersPerBatch; j++)
            {
                var generated = Player.New();

                await leaderboard.AddEntitiesAsync(leaderboardId, generated, fireAndForget: true);
                
                await leaderboard.UpdateEntityScoreAsync(leaderboardId, generated.Key, generated.Score, fireAndForget: true);
            }
        }
    }
}