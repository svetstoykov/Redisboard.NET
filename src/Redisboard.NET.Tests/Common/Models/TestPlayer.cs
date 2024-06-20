using Redisboard.NET.Interfaces;

namespace Redisboard.NET.Tests.Common.Models;

public class TestPlayer : ILeaderboardEntity
{
    public string Key { get; set; }

    public long Rank { get; set; }

    public double Score { get; set; }

    public string Username { get; set; }

    public DateTime EntryDate { get; set; }

    public static TestPlayer New()
    {
        var random = new Random();

        return new TestPlayer
        {
            Key = Guid.NewGuid().ToString(),
            EntryDate = DateTime.Now,
            Score = random.Next(),
            Username = $"user_{random.Next()}"
        };
    }
}