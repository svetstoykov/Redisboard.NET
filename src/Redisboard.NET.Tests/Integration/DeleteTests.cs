using FluentAssertions;

namespace Redisboard.NET.Tests.Integration;

/// <summary>
/// Tests for <see cref="Leaderboard.DeleteEntityAsync"/> (single entity) and
/// <see cref="Leaderboard.DeleteAsync"/> (entire leaderboard).
/// </summary>
public class DeleteTests : LeaderboardTestBase
{
    public DeleteTests(LeaderboardFixture fixture) : base(fixture) { }

    [Fact]
    public async Task DeleteEntityAsync_RemovesEntityFromAllDataStructures()
    {
        await SeedAsync([("del_top", 100.0), ("del_mid", 90.0), ("del_bot", 80.0)]);

        await Leaderboard.DeleteEntityAsync(Key, "del_top");

        var rank = await Leaderboard.GetEntityRankAsync(Key, "del_top");
        var score = await Leaderboard.GetEntityScoreAsync(Key, "del_top");
        var neighbours = await Leaderboard.GetEntityAndNeighboursAsync(Key, "del_mid", offset: 10);

        rank.Should().BeNull();
        score.Should().BeNull();
        neighbours.Should().NotContain(e => e.Key == "del_top");
    }

    [Fact]
    public async Task DeleteEntityAsync_RemainingEntitiesRetainCorrectRanks()
    {
        await SeedAsync([("keep_a", 300.0), ("keep_b", 200.0), ("keep_c", 100.0), ("remove", 150.0)]);

        await Leaderboard.DeleteEntityAsync(Key, "remove");

        var rankA = await Leaderboard.GetEntityRankAsync(Key, "keep_a");
        var rankB = await Leaderboard.GetEntityRankAsync(Key, "keep_b");
        var rankC = await Leaderboard.GetEntityRankAsync(Key, "keep_c");

        rankA.Should().Be(1);
        rankB.Should().Be(2);
        rankC.Should().Be(3);
    }

    [Fact]
    public async Task DeleteAsync_RemovesEntireLeaderboard()
    {
        await SeedAsync(Enumerable.Range(0, 50).Select(i => ($"d_{i}", (double)i)));

        await Leaderboard.DeleteAsync(Key);

        var rank = await Leaderboard.GetEntityRankAsync(Key, "d_25");
        var score = await Leaderboard.GetEntityScoreAsync(Key, "d_25");
        var neighbours = await Leaderboard.GetEntityAndNeighboursAsync(Key, "d_25", offset: 5);

        rank.Should().BeNull();
        score.Should().BeNull();
        neighbours.Should().BeEmpty();
    }
}
