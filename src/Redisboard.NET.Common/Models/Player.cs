using Redisboard.NET.Interfaces;

namespace Redisboard.NET.Common.Models;

public class Player : ILeaderboardEntity
{
    public string Key { get; set; }

    public long Rank { get; set; }

    public double Score { get; set; }

    public string Username { get; set; }

    public string FirstName { get; set; }
    
    public string LastName { get; set; }
    
    public DateTime EntryDate { get; set; }

    public static Player New()
    {
        var random = new Random();

        return new Player
        {
            Key = Guid.NewGuid().ToString(),
            EntryDate = DateTime.Now,
            Score = random.Next(),
            Username = $"user_{random.Next()}",
            FirstName = $"first_{random.Next()}",
            LastName = $"last_{random.Next()}"
        };
    }
}