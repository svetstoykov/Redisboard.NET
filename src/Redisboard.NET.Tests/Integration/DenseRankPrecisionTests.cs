using Redisboard.NET.Common.Models;
using Redisboard.NET.Enumerations;

namespace Redisboard.NET.Tests.Integration;

/// <summary>Verifies dense-rank reads preserve the exact scores accepted by Redis.</summary>
/// <remarks>
/// Exercises the public read APIs against real Redis with isolated leaderboard keys, including
/// scores that Lua's default number formatting rounds or renders differently from Redis.
/// </remarks>
public class DenseRankPrecisionTests : LeaderboardTestBase
{
    /// <summary>Initializes precision regressions using the shared Redis fixture.</summary>
    /// <param name="fixture">Provides the Redis-backed leaderboard used by each isolated test.</param>
    public DenseRankPrecisionTests(LeaderboardFixture fixture) : base(fixture) { }

    /// <summary>Gets finite scores covering zero, computed fractions, and double-precision boundaries.</summary>
    public static TheoryData<double> PrecisionSensitiveScores => new()
    {
        0d,
        0.1 + 0.2,
        1e14,
        1e15,
        1e-20,
        double.Epsilon,
        double.MaxValue
    };

    /// <summary>Verifies rank-range reads return the exact score and dense rank.</summary>
    /// <param name="score">Score whose Redis representation must remain usable as an index member.</param>
    /// <returns>A task that completes after checking the returned entity.</returns>
    [Theory]
    [MemberData(nameof(PrecisionSensitiveScores))]
    public async Task GetEntitiesByRankRangeAsync_PrecisionSensitiveScore_ReturnsExactScoreAndRank(double score)
    {
        using var culture = new InvariantCultureScope();
        await Leaderboard.AddEntityAsync(Key, new Player { Id = "p1", Score = score });

        var result = await Leaderboard.GetEntitiesByRankRangeAsync(Key, 1, 1, RankingType.DenseRank);

        var player = Assert.Single(result);
        Assert.Equal("p1", player.Id);
        Assert.Equal(score, player.Score);
        Assert.Equal(1L, player.Rank);
    }

    /// <summary>Verifies score-range reads return the exact score and dense rank.</summary>
    /// <param name="score">Score used as both inclusive bounds of the requested range.</param>
    /// <returns>A task that completes after checking the returned entity.</returns>
    [Theory]
    [MemberData(nameof(PrecisionSensitiveScores))]
    public async Task GetEntitiesByScoreRangeAsync_PrecisionSensitiveScore_ReturnsExactScoreAndRank(double score)
    {
        using var culture = new InvariantCultureScope();
        await Leaderboard.AddEntityAsync(Key, new Player { Id = "p1", Score = score });

        var result = await Leaderboard.GetEntitiesByScoreRangeAsync(Key, score, score, RankingType.DenseRank);

        var player = Assert.Single(result);
        Assert.Equal("p1", player.Id);
        Assert.Equal(score, player.Score);
        Assert.Equal(1L, player.Rank);
    }

    /// <summary>Verifies neighborhood reads return the exact score and dense rank.</summary>
    /// <param name="score">Score stored for the entity at the center of the requested window.</param>
    /// <returns>A task that completes after checking the returned entity.</returns>
    [Theory]
    [MemberData(nameof(PrecisionSensitiveScores))]
    public async Task GetEntityAndNeighboursAsync_PrecisionSensitiveScore_ReturnsExactScoreAndRank(double score)
    {
        using var culture = new InvariantCultureScope();
        await Leaderboard.AddEntityAsync(Key, new Player { Id = "p1", Score = score });

        var result = await Leaderboard.GetEntityAndNeighboursAsync(Key, "p1", 1, RankingType.DenseRank);

        var player = Assert.Single(result);
        Assert.Equal("p1", player.Id);
        Assert.Equal(score, player.Score);
        Assert.Equal(1L, player.Rank);
    }

    /// <summary>Verifies single-entity rank reads can resolve precision-sensitive scores.</summary>
    /// <param name="score">Score stored for the entity whose rank is requested.</param>
    /// <returns>A task that completes after checking the dense rank.</returns>
    [Theory]
    [MemberData(nameof(PrecisionSensitiveScores))]
    public async Task GetEntityRankAsync_PrecisionSensitiveScore_ReturnsDenseRank(double score)
    {
        using var culture = new InvariantCultureScope();
        await Leaderboard.AddEntityAsync(Key, new Player { Id = "p1", Score = score });

        var rank = await Leaderboard.GetEntityRankAsync(Key, "p1", RankingType.DenseRank);

        Assert.Equal(1L, rank);
    }

    /// <summary>Verifies adjacent doubles remain separate score groups while equal scores share ranks.</summary>
    /// <returns>A task that completes after checking score ordering and dense ranks.</returns>
    [Fact]
    public async Task AddEntitiesAsync_AdjacentScores_AssignsSeparateDenseRanks()
    {
        using var culture = new InvariantCultureScope();
        var higherScore = Math.BitIncrement(0.3);
        Player[] players =
        [
            new() { Id = "higher", Score = higherScore },
            new() { Id = "lower", Score = 0.3 },
            new() { Id = "tie", Score = higherScore },
            new() { Id = "bottom", Score = 0 }
        ];

        await Leaderboard.AddEntitiesAsync(Key, players);

        var result = await Leaderboard.GetEntitiesByRankRangeAsync(Key, 1, 5, RankingType.DenseRank);
        Assert.Equal(
            new[] { ("higher", 1L), ("tie", 1L), ("lower", 2L), ("bottom", 3L) },
            result.Select(player => (player.Id, player.Rank)).ToArray());
        Assert.Equal(new[] { higherScore, higherScore, 0.3, 0 }, result.Select(player => player.Score).ToArray());
    }
}
