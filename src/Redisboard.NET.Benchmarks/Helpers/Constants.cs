namespace Redisboard.NET.Benchmarks.Helpers;

internal static class Constants
{
    internal const int BenchmarkDbInstance = 8;
    
    internal const int LeaderboardPlayerCount = 500_000;
    
    internal const string LeaderboardKey = nameof(Benchmarks);
}