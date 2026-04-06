using Redisboard.NET.Common.Models;

namespace Redisboard.NET.DemoAPI.Models;

/// <summary>
/// API response model for player leaderboard entries.
/// Fields are mapped directly from <see cref="Player"/> — no manual deserialization needed.
/// </summary>
public class PlayerResponse
{
    public string Id { get; set; }
    public long Rank { get; set; }
    public double Score { get; set; }
    public string Username { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public DateTime EntryDate { get; set; }

    public static PlayerResponse From(Player player)
        => new()
        {
            Id = player.Id,
            Rank = player.Rank,
            Score = player.Score,
            Username = player.Username,
            FirstName = player.FirstName,
            LastName = player.LastName,
            EntryDate = player.EntryDate
        };
}
