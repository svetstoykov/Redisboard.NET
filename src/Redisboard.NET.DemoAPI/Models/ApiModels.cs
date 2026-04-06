namespace Redisboard.NET.DemoAPI.Models;

/// <summary>Request body for adding a new player to a leaderboard.</summary>
public class AddPlayerRequest
{
    public string Id { get; set; }
    public double Score { get; set; }
    public string Username { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
}

public class UpdateScoreRequest
{
    public double NewScore { get; set; }
}

public class EntityResponse
{
    public string Key { get; set; }
    public double? Score { get; set; }
    public long? Rank { get; set; }
}

public class SizeResponse
{
    public long Size { get; set; }
}
