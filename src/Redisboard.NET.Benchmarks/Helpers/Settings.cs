namespace Redisboard.NET.Benchmarks.Helpers;

internal static class Settings
{
    internal const int BenchmarkDbInstance = 8;
    
    internal const int LeaderboardPlayerCount = 500_000;
    
    internal static string LeaderboardKey() => $"{nameof(Benchmarks)}_{LeaderboardPlayerCount}_Players";
}