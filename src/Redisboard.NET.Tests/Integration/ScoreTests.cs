using FluentAssertions;

namespace Redisboard.NET.Tests.Integration;

/// <summary>
/// Tests for <see cref="Leaderboard.GetEntityScoreAsync"/> and
/// <see cref="Leaderboard.UpdateEntityScoreAsync"/>.
/// Covers retrieval, precision, updates, and rank transitions after score changes.
/// </summary>
public class ScoreTests : LeaderboardTestBase
{
    public ScoreTests(LeaderboardFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetEntityScoreAsync_ReturnsInsertedScore()
    {
        await SeedAsync([("sc_player", 742.0)]);

        var result = await Leaderboard.GetEntityScoreAsync(Key, "sc_player");

        result.Should().Be(742.0);
    }

    [Fact]
    public async Task GetEntityScoreAsync_DecimalScore_ReturnsPreciseValue()
    {
        await SeedAsync([("decimal_player", 3.14159)]);

        var result = await Leaderboard.GetEntityScoreAsync(Key, "decimal_player");

        result.Should().BeApproximately(3.14159, precision: 1e-5);
    }

    [Fact]
    public async Task GetEntityScoreAsync_NonExistentEntity_ReturnsNull()
    {
        await SeedAsync([("existing_sc", 100.0)]);

        var result = await Leaderboard.GetEntityScoreAsync(Key, "ghost_sc");

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateEntityScoreAsync_ReturnsUpdatedScore()
    {
        await SeedAsync([("Mike", 200), ("Alex", 100), ("John", 100), ("Sam", 50)]);

        await Leaderboard.UpdateEntityScoreAsync(Key, "John", 150);

        var result = await Leaderboard.GetEntityScoreAsync(Key, "John");

        result.Should().Be(150);
    }

    [Fact]
    public async Task UpdateEntityScoreAsync_MultipleUpdates_FinalScoreAndRankAreCorrect()
    {
        await SeedAsync([("evolve", 10.0), ("comp_a", 50.0), ("comp_b", 30.0), ("comp_c", 20.0)]);

        await Leaderboard.UpdateEntityScoreAsync(Key, "evolve", 40);
        await Leaderboard.UpdateEntityScoreAsync(Key, "evolve", 100);

        var score = await Leaderboard.GetEntityScoreAsync(Key, "evolve");
        var rank = await Leaderboard.GetEntityRankAsync(Key, "evolve");

        score.Should().Be(100);
        rank.Should().Be(1);
    }

    [Fact]
    public async Task GetEntityAndNeighboursAsync_ScoresMatchInsertedValues()
    {
        await SeedAsync([("sa", 999.5), ("sb", 500.25), ("sc", 1.0)]);

        var result = await Leaderboard.GetEntityAndNeighboursAsync(Key, "sb", offset: 5);

        result.First(e => e.Key == "sa").Score.Should().BeApproximately(999.5, 1e-5);
        result.First(e => e.Key == "sb").Score.Should().BeApproximately(500.25, 1e-5);
        result.First(e => e.Key == "sc").Score.Should().BeApproximately(1.0, 1e-5);
    }
}
