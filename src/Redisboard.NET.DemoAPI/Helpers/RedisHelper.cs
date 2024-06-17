using Redisboard.NET.DemoAPI.Models;
using Redisboard.NET.Interfaces;

namespace Redisboard.NET.DemoAPI.Helpers;

public class RedisHelper
{
    private static readonly Random Random = new();
    
    public static async Task SeedAsync(
        WebApplication app,
        object leaderboardId,
        int playersCount)
    {
        var scope = app.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<ILeaderboardManager<Player>>();

        const int batchInsertRepeat = 100;

        var playersPerBatch = playersCount / 100;

        for (var i = 0; i < batchInsertRepeat; i++)
        {
            var playersToAdd = new Player[playersPerBatch];

            for (var j = 0; j < playersPerBatch; j++)
            {
                var generated = new Player()
                {
                    Id = Guid.NewGuid().ToString(),
                    EntryDate = DateTime.Now,
                    FirstName = $"FirstName_{i}_{j}",
                    LastName = $"LastName_{i}_{j}",
                    Score = Random.Next(1, playersCount),
                    Username = $"user_{i}_{j}"
                };

                playersToAdd[j] = generated;
            }

            await manager.AddEnitityToLeaderboardAsync(leaderboardId, playersToAdd);
        }
    }
}