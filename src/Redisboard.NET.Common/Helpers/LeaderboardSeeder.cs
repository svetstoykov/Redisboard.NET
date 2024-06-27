using Redisboard.NET.Common.Models;
using Redisboard.NET.Interfaces;

namespace Redisboard.NET.Common.Helpers;

public static class LeaderboardSeeder
{
    public static async Task SeedAsync(
        ILeaderboard<Player> leaderboard,
        object leaderboardId,
        int playersCount)
    {
        const int batchInsertRepeat = 100;

        var playersPerBatch = playersCount / 100;

        for (var i = 0; i < batchInsertRepeat; i++)
        {
            var playersToAdd = new Player[playersPerBatch];

            for (var j = 0; j < playersPerBatch; j++)
            {
                var generated = Player.New();

                playersToAdd[j] = generated;
            }

            await leaderboard.AddEntitiesAsync(leaderboardId, playersToAdd);
        }
    }
}