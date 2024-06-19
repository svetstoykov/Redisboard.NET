using Redisboard.NET.Interfaces;

namespace Redisboard.NET.DemoAPI.Models;

public class Player : ILeaderboardEntity
{
    public Guid Id { get; set; }

    public string Key => Id.ToString();
    
    public long Rank { get; set; }
    
    public double Score { get; set; }
    
    public string Username { get; set; }
    
    public DateTime EntryDate { get; set; }
    
    public string FirstName { get; set; }
    
    public string LastName { get; set; }
}