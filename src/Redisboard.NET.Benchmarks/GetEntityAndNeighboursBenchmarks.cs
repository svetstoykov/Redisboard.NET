using BenchmarkDotNet.Attributes;
using Redisboard.NET.Benchmarks.Helpers;
using Redisboard.NET.Common.Models;
using Redisboard.NET.Enumerations;
using StackExchange.Redis;

namespace Redisboard.NET.Benchmarks;

[MemoryDiagnoser]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
public class GetEntityAndNeighboursBenchmarks
{
    private Leaderboard _leaderboard;
    private Player _benchmarkPlayer;

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        var connection = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
        var db = connection.GetDatabase(Settings.BenchmarkDbInstance);

        _leaderboard = new Leaderboard(db);
        
        _benchmarkPlayer = Player.New();
        
        await _leaderboard.AddEntityAsync(Settings.LeaderboardKey(), _benchmarkPlayer);
    }
    
    [Benchmark(Baseline = true)]
    [ArgumentsSource(nameof(Data))]
    public async Task GetEntityAndNeighbours_DefaultRanking_500K_Players(int offset)
    {
        await _leaderboard.GetEntityAndNeighboursAsync(
            Settings.LeaderboardKey(), 
            _benchmarkPlayer.Key, 
            offset,
            RankingType.Default);
    }
    
    [Benchmark]
    [ArgumentsSource(nameof(Data))]
    public async Task GetEntityAndNeighbours_DenseRanking_500K_Players(int offset)
    {
        await _leaderboard.GetEntityAndNeighboursAsync(
            Settings.LeaderboardKey(), 
            _benchmarkPlayer.Key, 
            offset,
            RankingType.DenseRank);
    }
    
    [Benchmark]
    [ArgumentsSource(nameof(Data))]
    public async Task GetEntityAndNeighbours_Competition_500K_Players(int offset)
    {
        await _leaderboard.GetEntityAndNeighboursAsync(
            Settings.LeaderboardKey(), 
            _benchmarkPlayer.Key, 
            offset,
            RankingType.StandardCompetition);
    }
    
    public IEnumerable<object[]> Data()
    {
        yield return [10];
        yield return [20];
        yield return [50];
    }
}