using FluentAssertions;
using Redisboard.NET.Common.Models;

namespace Redisboard.NET.Tests.Integration;

/// <summary>
/// Tests for <see cref="Leaderboard{T}.GetEntityScoreAsync"/> and
/// <see cref="Leaderboard{T}.UpdateEntityScoreAsync"/>.
/// Covers retrieval, precision, updates, and rank transitions after score changes.
/// </summary>
public class ScoreTests : LeaderboardTestBase
{
    public ScoreTests(LeaderboardFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetEntityScoreAsync_ReturnsInsertedScore()
    {
        // Arrange
        await SeedAsync([("sc_player", 742.0)]);

        // Act
        var result = await Leaderboard.GetEntityScoreAsync(Key, "sc_player");

        // Assert
        result.Should().Be(742.0);
    }

    [Fact]
    public async Task GetEntityScoreAsync_DecimalScore_ReturnsPreciseValue()
    {
        // Arrange
        await SeedAsync([("decimal_player", 3.14159)]);

        // Act
        var result = await Leaderboard.GetEntityScoreAsync(Key, "decimal_player");

        // Assert
        result.Should().BeApproximately(3.14159, precision: 1e-5);
    }

    [Fact]
    public async Task GetEntityScoreAsync_NonExistentEntity_ReturnsNull()
    {
        // Arrange
        await SeedAsync([("existing_sc", 100.0)]);

        // Act
        var result = await Leaderboard.GetEntityScoreAsync(Key, "ghost_sc");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateEntityScoreAsync_ReturnsUpdatedScore()
    {
        // Arrange
        await SeedAsync([("Mike", 200), ("Alex", 100), ("John", 100), ("Sam", 50)]);

        // Act
        var result = await RoundTripAsync(
            mutate: () => Leaderboard.UpdateEntityScoreAsync(Key, LeaderboardSeed.Player("John", 150)),
            observe: () => Leaderboard.GetEntityScoreAsync(Key, "John"));

        // Assert
        result.Should().Be(150);
    }

    [Fact]
    public async Task UpdateEntityScoreAsync_MultipleUpdates_FinalScoreAndRankAreCorrect()
    {
        // Arrange
        await SeedAsync([("evolve", 10.0), ("comp_a", 50.0), ("comp_b", 30.0), ("comp_c", 20.0)]);

        // Act
        await Leaderboard.UpdateEntityScoreAsync(Key, new Player { Id = "evolve", Score = 40 });
        await Leaderboard.UpdateEntityScoreAsync(Key, new Player { Id = "evolve", Score = 100 });

        // Assert
        var score = await Leaderboard.GetEntityScoreAsync(Key, "evolve");
        var rank = await Leaderboard.GetEntityRankAsync(Key, "evolve");

        score.Should().Be(100);
        rank.Should().Be(1);
    }

    [Fact]
    public async Task GetEntityAndNeighboursAsync_ScoresMatchInsertedValues()
    {
        // Arrange
        await SeedAsync([("sa", 999.5), ("sb", 500.25), ("sc", 1.0)]);

        // Act
        var result = await Leaderboard.GetEntityAndNeighboursAsync(Key, "sb", offset: 5);

        // Assert
        result.First(e => e.Id == "sa").Score.Should().BeApproximately(999.5, 1e-5);
        result.First(e => e.Id == "sb").Score.Should().BeApproximately(500.25, 1e-5);
        result.First(e => e.Id == "sc").Score.Should().BeApproximately(1.0, 1e-5);
    }
}
