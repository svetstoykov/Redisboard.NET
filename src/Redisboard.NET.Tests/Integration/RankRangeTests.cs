using FluentAssertions;
using Redisboard.NET.Enumerations;

namespace Redisboard.NET.Tests.Integration;

/// <summary>
/// Tests for <see cref="Leaderboard.GetEntitiesByRankRangeAsync"/> across all ranking types,
/// including empty leaderboards, exact ranges, and out-of-bounds ranges.
/// </summary>
public class RankRangeTests : LeaderboardTestBase
{
    public RankRangeTests(LeaderboardFixture fixture) : base(fixture) { }

    // Basic behaviour

    [Fact]
    public async Task GetEntitiesByRankRangeAsync_Top3_ReturnsCorrectEntities()
    {
        await SeedAsync([("A", 500), ("B", 400), ("C", 300), ("D", 200), ("E", 100)]);

        var result = await Leaderboard.GetEntitiesByRankRangeAsync(Key, 1, 3);

        result.Should().HaveCount(3);
        result[0].Key.Should().Be("A");
        result[0].Rank.Should().Be(1);
        result[1].Key.Should().Be("B");
        result[1].Rank.Should().Be(2);
        result[2].Key.Should().Be("C");
        result[2].Rank.Should().Be(3);
    }

    [Fact]
    public async Task GetEntitiesByRankRangeAsync_MiddlePage_ReturnsCorrectEntities()
    {
        await SeedAsync([("A", 500), ("B", 400), ("C", 300), ("D", 200), ("E", 100)]);

        var result = await Leaderboard.GetEntitiesByRankRangeAsync(Key, 2, 4);

        result.Should().HaveCount(3);
        result.Select(e => (string)e.Key).Should().ContainInOrder("B", "C", "D");
    }

    [Fact]
    public async Task GetEntitiesByRankRangeAsync_SingleRank_ReturnsSingleEntity()
    {
        await SeedAsync([("A", 500), ("B", 400), ("C", 300)]);

        var result = await Leaderboard.GetEntitiesByRankRangeAsync(Key, 2, 2);

        result.Should().HaveCount(1);
        result[0].Key.Should().Be("B");
        result[0].Rank.Should().Be(2);
    }

    // Empty leaderboard

    [Fact]
    public async Task GetEntitiesByRankRangeAsync_EmptyLeaderboard_ReturnsEmpty()
    {
        var result = await Leaderboard.GetEntitiesByRankRangeAsync(Key, 1, 10);

        result.Should().BeEmpty();
    }

    // Out-of-bounds ranges — should return only existing entries

    [Fact]
    public async Task GetEntitiesByRankRangeAsync_RangeExceedsSize_ReturnsOnlyExistingEntries()
    {
        await SeedAsync([("A", 500), ("B", 400), ("C", 300)]);

        var result = await Leaderboard.GetEntitiesByRankRangeAsync(Key, 1, 100);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetEntitiesByRankRangeAsync_StartBeyondSize_ReturnsEmpty()
    {
        await SeedAsync([("A", 500), ("B", 400), ("C", 300)]);

        var result = await Leaderboard.GetEntitiesByRankRangeAsync(Key, 50, 100);

        result.Should().BeEmpty();
    }

    // All 4 ranking types
    //
    // Dataset: p1=300, p2=300, p3=200, p4=200, p5=100
    // Query ranks 1–5 to verify ranking assignment per type

    [Fact]
    public async Task GetEntitiesByRankRangeAsync_DefaultRanking_SequentialRanks()
    {
        await SeedAsync([("p1", 300), ("p2", 300), ("p3", 200), ("p4", 200), ("p5", 100)]);

        var result = await Leaderboard.GetEntitiesByRankRangeAsync(Key, 1, 5, RankingType.Default);

        result.Should().HaveCount(5);
        result.Select(e => e.Rank).Should().ContainInOrder(1, 2, 3, 4, 5);
    }

    [Fact]
    public async Task GetEntitiesByRankRangeAsync_DenseRanking_TiedPlayersShareRank()
    {
        await SeedAsync([("p1", 300), ("p2", 300), ("p3", 200), ("p4", 200), ("p5", 100)]);

        var result = await Leaderboard.GetEntitiesByRankRangeAsync(Key, 1, 5, RankingType.DenseRank);

        result.Should().HaveCount(5);

        // Dense: ties share rank, no gaps → [1,1,2,2,3]
        result.Where(e => e.Score == 300).Should().OnlyContain(e => e.Rank == 1);
        result.Where(e => e.Score == 200).Should().OnlyContain(e => e.Rank == 2);
        result.Where(e => e.Score == 100).Should().OnlyContain(e => e.Rank == 3);
    }

    [Fact]
    public async Task GetEntitiesByRankRangeAsync_StandardCompetition_TiedPlayersShareRankWithGapsAfter()
    {
        await SeedAsync([("p1", 300), ("p2", 300), ("p3", 200), ("p4", 200), ("p5", 100)]);

        var result = await Leaderboard.GetEntitiesByRankRangeAsync(Key, 1, 5, RankingType.StandardCompetition);

        result.Should().HaveCount(5);

        // SCR: ties share lowest rank, gaps after → [1,1,3,3,5]
        result.Where(e => e.Score == 300).Should().OnlyContain(e => e.Rank == 1);
        result.Where(e => e.Score == 200).Should().OnlyContain(e => e.Rank == 3);
        result.Where(e => e.Score == 100).Should().OnlyContain(e => e.Rank == 5);
    }

    [Fact]
    public async Task GetEntitiesByRankRangeAsync_ModifiedCompetition_TiedPlayersShareRankWithGapsBefore()
    {
        await SeedAsync([("p1", 300), ("p2", 300), ("p3", 200), ("p4", 200), ("p5", 100)]);

        var result = await Leaderboard.GetEntitiesByRankRangeAsync(Key, 1, 5, RankingType.ModifiedCompetition);

        result.Should().HaveCount(5);

        // MCR: ties share highest rank, gaps before → [2,2,4,4,5]
        result.Where(e => e.Score == 300).Should().OnlyContain(e => e.Rank == 2);
        result.Where(e => e.Score == 200).Should().OnlyContain(e => e.Rank == 4);
        result.Where(e => e.Score == 100).Should().OnlyContain(e => e.Rank == 5);
    }

    // Rank consistency with GetEntityRankAsync

    [Fact]
    public async Task GetEntitiesByRankRangeAsync_RanksMatchGetEntityRankAsync()
    {
        await SeedAsync([("A", 500), ("B", 400), ("C", 300), ("D", 200), ("E", 100)]);

        var rangeResult = await Leaderboard.GetEntitiesByRankRangeAsync(Key, 1, 5);

        foreach (var entity in rangeResult)
        {
            var individualRank = await Leaderboard.GetEntityRankAsync(Key, entity.Key);
            entity.Rank.Should().Be(individualRank!.Value,
                because: $"rank of {entity.Key} from range query should match individual rank query");
        }
    }
}
