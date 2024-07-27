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
    private Leaderboard<Player> _leaderboard;
    private Player _benchmarkPlayer;

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        var connection = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
        var db = connection.GetDatabase(Constants.BenchmarkDbInstance);

        _leaderboard = new Leaderboard<Player>(db);
        
        _benchmarkPlayer = Player.New();

        await _leaderboard.AddEntitiesAsync(Constants.LeaderboardKey, _benchmarkPlayer);
    }
    
    [Benchmark(Baseline = true)]
    [ArgumentsSource(nameof(Data))]
    public async Task GetEntityAndNeighbours_DefaultRanking_500K_Players(int offset)
    {
        await _leaderboard.GetEntityAndNeighboursAsync(
            Constants.LeaderboardKey, 
            _benchmarkPlayer.Key, 
            offset,
            RankingType.Default);
    }
    
    [Benchmark]
    [ArgumentsSource(nameof(Data))]
    public async Task GetEntityAndNeighbours_DenseRanking_500K_Players(int offset)
    {
        await _leaderboard.GetEntityAndNeighboursAsync(
            Constants.LeaderboardKey, 
            _benchmarkPlayer.Key, 
            offset,
            RankingType.DenseRank);
    }
    
    [Benchmark]
    [ArgumentsSource(nameof(Data))]
    public async Task GetEntityAndNeighbours_Competition_500K_Players(int offset)
    {
        await _leaderboard.GetEntityAndNeighboursAsync(
            Constants.LeaderboardKey, 
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