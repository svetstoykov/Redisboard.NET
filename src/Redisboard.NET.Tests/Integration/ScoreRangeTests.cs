using FluentAssertions;
using Redisboard.NET.Enumerations;

namespace Redisboard.NET.Tests.Integration;

/// <summary>
/// Tests for <see cref="Leaderboard{T}.GetEntitiesByScoreRangeAsync"/> across all ranking types,
/// including rank-value verification for tied scores and out-of-range queries.
/// </summary>
public class ScoreRangeTests : LeaderboardTestBase
{
    public ScoreRangeTests(LeaderboardFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetEntitiesByScoreRangeAsync_ReturnsEntitiesInRange()
    {
        await SeedAsync([("Mike", 200), ("Alex", 100), ("John", 100), ("Sam", 50), ("Jim", 20)]);

        var result = await Leaderboard.GetEntitiesByScoreRangeAsync(Key, 50, 100);

        result.Should().HaveCount(3);
        result.Select(r => r.Id).Should().Contain(["Alex", "John", "Sam"]);
    }

    [Fact]
    public async Task GetEntitiesByScoreRangeAsync_OutOfRange_ReturnsEmpty()
    {
        await SeedAsync([("Mike", 200), ("Alex", 100), ("John", 100), ("Sam", 50), ("Jim", 20)]);

        var result = await Leaderboard.GetEntitiesByScoreRangeAsync(Key, 205, 300);

        result.Should().BeEmpty();
    }

    // Rank verification per ranking type
    //
    // Dataset: top1=300, top2=300, mid1=200, mid2=200, low1=100
    // Query range: [100, 200] → returns mid1, mid2, low1

    [Fact]
    public async Task GetEntitiesByScoreRangeAsync_AllRankingTypes_CorrectEntitiesReturned()
    {
        await SeedAsync([("top1", 300.0), ("top2", 300.0), ("mid1", 200.0), ("mid2", 200.0), ("low1", 100.0)]);

        foreach (var rankingType in Enum.GetValues<RankingType>())
        {
            var result = await Leaderboard.GetEntitiesByScoreRangeAsync(Key, 100, 200, rankingType);

            result.Should().HaveCount(3, because: $"{rankingType} should return 3 entities");
            result.Select(e => e.Id).Should().Contain(["mid1", "mid2", "low1"],
                because: $"{rankingType} should include all in-range entities");
        }
    }

    [Fact]
    public async Task GetEntitiesByScoreRangeAsync_DenseRanking_CorrectRanks()
    {
        await SeedAsync([("top1", 300.0), ("top2", 300.0), ("mid1", 200.0), ("mid2", 200.0), ("low1", 100.0)]);

        var result = await Leaderboard.GetEntitiesByScoreRangeAsync(Key, 100, 200, RankingType.DenseRank);

        result.Where(e => e.Id == "mid1" || e.Id == "mid2")
            .Should().OnlyContain(e => e.Rank == 2);

        result.First(e => e.Id == "low1").Rank.Should().Be(3);
    }

    [Fact]
    public async Task GetEntitiesByScoreRangeAsync_StandardCompetition_CorrectRanks()
    {
        await SeedAsync([("top1", 300.0), ("top2", 300.0), ("mid1", 200.0), ("mid2", 200.0), ("low1", 100.0)]);

        var result = await Leaderboard.GetEntitiesByScoreRangeAsync(Key, 100, 200, RankingType.StandardCompetition);

        result.Where(e => e.Id == "mid1" || e.Id == "mid2")
            .Should().OnlyContain(e => e.Rank == 3);

        result.First(e => e.Id == "low1").Rank.Should().Be(5);
    }

    [Fact]
    public async Task GetEntitiesByScoreRangeAsync_ModifiedCompetition_CorrectRanks()
    {
        await SeedAsync([("top1", 300.0), ("top2", 300.0), ("mid1", 200.0), ("mid2", 200.0), ("low1", 100.0)]);

        var result = await Leaderboard.GetEntitiesByScoreRangeAsync(Key, 100, 200, RankingType.ModifiedCompetition);

        result.Where(e => e.Id == "mid1" || e.Id == "mid2")
            .Should().OnlyContain(e => e.Rank == 4);

        result.First(e => e.Id == "low1").Rank.Should().Be(5);
    }
}
