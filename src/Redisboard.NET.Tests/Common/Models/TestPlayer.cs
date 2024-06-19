using Redisboard.NET.Interfaces;

namespace Redisboard.NET.Tests.Common.Models;

public class TestPlayer : ILeaderboardEntity
{
    public string Id { get; set; }

    public long Rank { get; set; }

    public double Score { get; set; }

    public string Username { get; set; }

    public DateTime EntryDate { get; set; }

    public string FirstName { get; set; }

    public string LastName { get; set; }

    public static TestPlayer New()
    {
        var random = new Random();

        return new TestPlayer
        {
            Id = Guid.NewGuid().ToString(),
            EntryDate = DateTime.Now,
            FirstName = $"FirstName_{random.Next()}",
            LastName = $"LastName_{random.Next()}",
            Score = random.Next(),
            Username = $"user_{random.Next()}"
        };
    }
}