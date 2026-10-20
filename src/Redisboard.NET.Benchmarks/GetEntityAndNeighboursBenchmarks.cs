using BenchmarkDotNet.Attributes;
using Redisboard.NET.Benchmarks.Helpers;
using Redisboard.NET.Common.Models;
using Redisboard.NET.Enumerations;
using Redisboard.NET.Serialization;
using StackExchange.Redis;

namespace Redisboard.NET.Benchmarks;

[MemoryDiagnoser]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
public class GetEntityAndNeighboursBenchmarks
{
    private Leaderboard<Player> _leaderboard;
    private Player _benchmarkPlayer;
    private ConnectionMultiplexer _connection;

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        await BenchmarkLeaderboardHelper.InitializeBenchmarksLeaderboardAsync();
        _connection = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
        var db = _connection.GetDatabase(Settings.BenchmarkDbInstance);

        _leaderboard = new Leaderboard<Player>(db, new MemoryPackLeaderboardSerializer());

        _benchmarkPlayer = Player.New();

        await _leaderboard.AddEntityAsync(Settings.LeaderboardKey(), _benchmarkPlayer);
    }

    /// <summary>Deletes benchmark data and closes the connection after the selected case.</summary>
    /// <returns>A task that completes after benchmark cleanup.</returns>
    [GlobalCleanup]
    public async Task GlobalCleanup()
    {
        await BenchmarkLeaderboardHelper.CleanUpBenchmarksLeaderboardAsync();
        await _connection.DisposeAsync();
    }

    [Benchmark(Baseline = true)]
    [ArgumentsSource(nameof(Data))]
    public async Task GetEntityAndNeighbours_DefaultRanking_500K_Players(int offset)
    {
        await _leaderboard.GetEntityAndNeighboursAsync(
            Settings.LeaderboardKey(),
            _benchmarkPlayer.Id,
            offset,
            RankingType.Default);
    }

    [Benchmark]
    [ArgumentsSource(nameof(Data))]
    public async Task GetEntityAndNeighbours_DenseRanking_500K_Players(int offset)
    {
        await _leaderboard.GetEntityAndNeighboursAsync(
            Settings.LeaderboardKey(),
            _benchmarkPlayer.Id,
            offset,
            RankingType.DenseRank);
    }

    [Benchmark]
    [ArgumentsSource(nameof(Data))]
    public async Task GetEntityAndNeighbours_Competition_500K_Players(int offset)
    {
        await _leaderboard.GetEntityAndNeighboursAsync(
            Settings.LeaderboardKey(),
            _benchmarkPlayer.Id,
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
