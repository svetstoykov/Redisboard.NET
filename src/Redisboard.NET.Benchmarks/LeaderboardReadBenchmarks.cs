using BenchmarkDotNet.Attributes;
using Redisboard.NET.Common.Models;
using Redisboard.NET.Enumerations;
using Redisboard.NET.Serialization;
using StackExchange.Redis;

namespace Redisboard.NET.Benchmarks;

/// <summary>Measures entity range reads and rank-only reads against an isolated Redis leaderboard.</summary>
/// <remarks>
/// Each run owns its Redis keys and includes ties so ranking modes exercise repeated score groups.
/// Results measure the selected checkout; compare identical jobs against the base branch for performance claims.
/// </remarks>
[MemoryDiagnoser]
public class LeaderboardReadBenchmarks
{
    private ConnectionMultiplexer _connection = null!;
    private Leaderboard<Player> _leaderboard = null!;
    private readonly string _key = $"read_benchmark_{Guid.NewGuid():N}";

    /// <summary>Gets or sets the number of entities in the benchmark leaderboard.</summary>
    [Params(10_000, 100_000)]
    public int PlayerCount { get; set; }

    /// <summary>Gets or sets the ranking mode used for every read.</summary>
    [Params(RankingType.Default, RankingType.DenseRank, RankingType.StandardCompetition, RankingType.ModifiedCompetition)]
    public RankingType Ranking { get; set; }

    /// <summary>Seeds deterministic data outside measured operations.</summary>
    /// <returns>A task that completes after seeding the isolated leaderboard.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _connection = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
        _leaderboard = new Leaderboard<Player>(_connection.GetDatabase(8), new MemoryPackLeaderboardSerializer());
        foreach (var batch in Enumerable.Range(0, PlayerCount).Chunk(1_000))
            await _leaderboard.AddEntitiesAsync(_key, batch.Select(i => new Player { Id = $"p{i:D8}", Score = i / 10, Username = "benchmark" }));
    }

    /// <summary>Deletes only this run's leaderboard and closes its connection.</summary>
    /// <returns>A task that completes after cleanup.</returns>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _leaderboard.DeleteAsync(_key);
        await _connection.DisposeAsync();
    }

    /// <summary>Reads a fixed rank page.</summary>
    /// <returns>A task containing fifty ranked entities.</returns>
    [Benchmark]
    public Task<Player[]> RankRange() => _leaderboard.GetEntitiesByRankRangeAsync(_key, 100, 149, Ranking);

    /// <summary>Reads a narrow score range containing tied scores.</summary>
    /// <returns>A task containing the entities within eleven score groups.</returns>
    [Benchmark]
    public Task<Player[]> ScoreRangeNarrow() => _leaderboard.GetEntitiesByScoreRangeAsync(_key, 100, 110, Ranking);

    /// <summary>Reads all entities to expose wide-range payload costs.</summary>
    /// <returns>A task containing the complete leaderboard.</returns>
    [Benchmark]
    public Task<Player[]> ScoreRangeWide() => _leaderboard.GetEntitiesByScoreRangeAsync(_key, 0, double.PositiveInfinity, Ranking);

    /// <summary>Reads one rank without entity metadata.</summary>
    /// <returns>A task containing the selected member's rank.</returns>
    [Benchmark]
    public Task<long?> EntityRank() => _leaderboard.GetEntityRankAsync(_key, $"p{PlayerCount / 2:D8}", Ranking);
}
