using System.Text.Json;
using Redisboard.NET.Common.Models;
using Redisboard.NET.Interfaces;

namespace Redisboard.NET.DemoAPI.Models;

public class PlayerResponse
{
    public string Key { get; set; }
    public long Rank { get; set; }
    public double Score { get; set; }
    public PlayerData Metadata { get; set; }

    public static PlayerResponse MapFromLeaderboardEntity(ILeaderboardEntity player)
        => new()
        {
            Key = player.Key,
            Rank = player.Rank,
            Score = player.Score,
            Metadata = JsonSerializer.Deserialize<PlayerData>(player.Metadata)
        };
}