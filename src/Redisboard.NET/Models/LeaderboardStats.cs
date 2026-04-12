namespace Redisboard.NET.Models;

internal readonly struct LeaderboardStats
{
    public LeaderboardStats(long rank, double score)
    {
        this.Rank = rank;
        this.Score = score;
    }
    public long Rank { get; }
    public double Score { get; }
}