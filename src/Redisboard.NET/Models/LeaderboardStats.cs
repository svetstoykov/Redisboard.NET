namespace Redisboard.NET.Models;

internal readonly struct LeaderboardStats
{
    public LeaderboardStats(long rank, double score)
    {
        Rank = rank;
        Score = score;
    }
    
    public long Rank { get; }
    public double Score { get; }
}