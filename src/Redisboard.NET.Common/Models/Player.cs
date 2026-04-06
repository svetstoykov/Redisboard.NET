using Redisboard.NET.Attributes;
using Redisboard.NET.Interfaces;

namespace Redisboard.NET.Common.Models;

/// <summary>
/// Example domain entity used by integration tests, benchmarks, and the DemoAPI.
/// </summary>
public class Player : ILeaderboardEntity
{
    /// <summary>Unique identifier for this player.</summary>
    [LeaderboardKey]
    public string Id { get; set; }

    /// <summary>The player's current score used for ranking.</summary>
    [LeaderboardScore]
    public double Score { get; set; }

    /// <inheritdoc />
    public long Rank { get; set; }

    // ---- Domain properties (persisted as metadata by the library) ----------

    public string Username { get; set; }

    public string FirstName { get; set; }

    public string LastName { get; set; }

    public DateTime EntryDate { get; set; }

    /// <summary>
    /// Factory that creates a random player instance for testing and seeding.
    /// </summary>
    public static Player New()
    {
        var random = new Random();

        return new Player
        {
            Id = Guid.NewGuid().ToString(),
            Score = random.Next(1, 25_000),
            Username = $"user_{random.Next()}",
            FirstName = $"first_{random.Next()}",
            LastName = $"last_{random.Next()}",
            EntryDate = DateTime.UtcNow
        };
    }
}
