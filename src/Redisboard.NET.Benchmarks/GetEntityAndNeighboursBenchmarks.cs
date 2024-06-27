using BenchmarkDotNet.Attributes;
using Redisboard.NET.Benchmarks.Helpers;
using Redisboard.NET.Common.Models;
using StackExchange.Redis;

namespace Redisboard.NET.Benchmarks;

[MemoryDiagnoser]
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

        await _leaderboard.AddEntitiesAsync(Constants.LeaderboardKey, [_benchmarkPlayer]);
    }
    
    [Benchmark]
    [ArgumentsSource(nameof(Data))]
    public async Task GetEntityAndNeighbours(int offset)
    {
        await _leaderboard.GetEntityAndNeighboursAsync(Constants.LeaderboardKey, _benchmarkPlayer.Key, offset);
    }
    
    public IEnumerable<object[]> Data()
    {
        yield return [10];
        yield return [20];
        yield return [50];
        yield return [100];
    }
}