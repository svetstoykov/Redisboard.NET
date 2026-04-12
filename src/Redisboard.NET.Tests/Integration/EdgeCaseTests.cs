using FluentAssertions;

namespace Redisboard.NET.Tests.Integration;

/// <summary>
/// Edge-case tests: non-existent entities, boundary offsets, and last-place offset clamping.
/// </summary>
public class EdgeCaseTests : LeaderboardTestBase
{
    public EdgeCaseTests(LeaderboardFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetEntityAndNeighboursAsync_NonExistentEntity_ReturnsEmpty()
    {
        // Arrange
        await SeedAsync([("real_player", 100.0)]);

        // Act
        var result = await Leaderboard.GetEntityAndNeighboursAsync(Key, "ghost", offset: 5);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEntityRankAsync_NonExistentEntity_ReturnsNull()
    {
        // Arrange
        await SeedAsync([("real_rank", 100.0)]);

        // Act
        var result = await Leaderboard.GetEntityRankAsync(Key, "nobody");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetEntityAndNeighboursAsync_LastPlacePlayer_OffsetClampedToAvailableEntities()
    {
        // Arrange
        await SeedAsync(Enumerable.Range(1, 10).Select(i => ($"last_{i}", (double)i)));

        // Act
        var result = await Leaderboard.GetEntityAndNeighboursAsync(Key, "last_1", offset: 3);

        // Assert
        result.Should().HaveCount(4);
        result.Should().Contain(e => e.Id == "last_1");
    }
}
