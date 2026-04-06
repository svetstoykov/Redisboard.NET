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
        await SeedAsync([("real_player", 100.0)]);

        var result = await Leaderboard.GetEntityAndNeighboursAsync(Key, "ghost", offset: 5);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEntityRankAsync_NonExistentEntity_ReturnsNull()
    {
        await SeedAsync([("real_rank", 100.0)]);

        var result = await Leaderboard.GetEntityRankAsync(Key, "nobody");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetEntityAndNeighboursAsync_LastPlacePlayer_OffsetClampedToAvailableEntities()
    {
        // last_1 has score 1 (lowest), so it is the last-place player.
        // With offset = 3 it should return last_1 + the 3 players above it = 4 total.
        await SeedAsync(Enumerable.Range(1, 10).Select(i => ($"last_{i}", (double)i)));

        var result = await Leaderboard.GetEntityAndNeighboursAsync(Key, "last_1", offset: 3);

        result.Should().HaveCount(4); // 3 neighbours above + itself
        result.Should().Contain(e => e.Id == "last_1");
    }
}
