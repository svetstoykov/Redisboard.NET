using FluentAssertions;
using Redisboard.NET.Enumerations;

namespace Redisboard.NET.Tests.Integration;

/// <summary>
/// Tests for <see cref="Leaderboard{T}.GetEntitiesByRankRangeAsync"/> across all ranking types,
/// including empty leaderboards, exact ranges, and out-of-bounds ranges.
/// </summary>
public class RankRangeTests : LeaderboardTestBase
{
    public RankRangeTests(LeaderboardFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetEntitiesByRankRangeAsync_Top3_ReturnsCorrectEntities()
    {
        // Arrange
        await SeedAsync([("A", 500), ("B", 400), ("C", 300), ("D", 200), ("E", 100)]);

        // Act
        var result = await Leaderboard.GetEntitiesByRankRangeAsync(Key, 1, 3);

        // Assert
        result.Should().HaveCount(3);
        result[0].Id.Should().Be("A");
        result[0].Rank.Should().Be(1);
        result[1].Id.Should().Be("B");
        result[1].Rank.Should().Be(2);
        result[2].Id.Should().Be("C");
        result[2].Rank.Should().Be(3);
    }

    [Fact]
    public async Task GetEntitiesByRankRangeAsync_MiddlePage_ReturnsCorrectEntities()
    {
        // Arrange
        await SeedAsync([("A", 500), ("B", 400), ("C", 300), ("D", 200), ("E", 100)]);

        // Act
        var result = await Leaderboard.GetEntitiesByRankRangeAsync(Key, 2, 4);

        // Assert
        result.Should().HaveCount(3);
        result.Select(e => e.Id).Should().ContainInOrder("B", "C", "D");
    }

    [Fact]
    public async Task GetEntitiesByRankRangeAsync_SingleRank_ReturnsSingleEntity()
    {
        // Arrange
        await SeedAsync([("A", 500), ("B", 400), ("C", 300)]);

        // Act
        var result = await Leaderboard.GetEntitiesByRankRangeAsync(Key, 2, 2);

        // Assert
        result.Should().HaveCount(1);
        result[0].Id.Should().Be("B");
        result[0].Rank.Should().Be(2);
    }

    [Fact]
    public async Task GetEntitiesByRankRangeAsync_EmptyLeaderboard_ReturnsEmpty()
    {
        // Act
        var result = await Leaderboard.GetEntitiesByRankRangeAsync(Key, 1, 10);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEntitiesByRankRangeAsync_RangeExceedsSize_ReturnsOnlyExistingEntries()
    {
        // Arrange
        await SeedAsync([("A", 500), ("B", 400), ("C", 300)]);

        // Act
        var result = await Leaderboard.GetEntitiesByRankRangeAsync(Key, 1, 100);

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetEntitiesByRankRangeAsync_StartBeyondSize_ReturnsEmpty()
    {
        // Arrange
        await SeedAsync([("A", 500), ("B", 400), ("C", 300)]);

        // Act
        var result = await Leaderboard.GetEntitiesByRankRangeAsync(Key, 50, 100);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEntitiesByRankRangeAsync_DefaultRanking_SequentialRanks()
    {
        // Arrange
        await SeedAsync([("p1", 300), ("p2", 300), ("p3", 200), ("p4", 200), ("p5", 100)]);

        // Act
        var result = await Leaderboard.GetEntitiesByRankRangeAsync(Key, 1, 5, RankingType.Default);

        // Assert
        result.Should().HaveCount(5);
        result.Select(e => e.Rank).Should().ContainInOrder(1, 2, 3, 4, 5);
    }

    [Fact]
    public async Task GetEntitiesByRankRangeAsync_DenseRanking_TiedPlayersShareRank()
    {
        // Arrange
        await SeedAsync([("p1", 300), ("p2", 300), ("p3", 200), ("p4", 200), ("p5", 100)]);

        // Act
        var result = await Leaderboard.GetEntitiesByRankRangeAsync(Key, 1, 5, RankingType.DenseRank);

        // Assert
        result.Should().HaveCount(5);
        result.Where(e => e.Score == 300).Should().OnlyContain(e => e.Rank == 1);
        result.Where(e => e.Score == 200).Should().OnlyContain(e => e.Rank == 2);
        result.Where(e => e.Score == 100).Should().OnlyContain(e => e.Rank == 3);
    }

    [Fact]
    public async Task GetEntitiesByRankRangeAsync_StandardCompetition_TiedPlayersShareRankWithGapsAfter()
    {
        // Arrange
        await SeedAsync([("p1", 300), ("p2", 300), ("p3", 200), ("p4", 200), ("p5", 100)]);

        // Act
        var result = await Leaderboard.GetEntitiesByRankRangeAsync(Key, 1, 5, RankingType.StandardCompetition);

        // Assert
        result.Should().HaveCount(5);
        result.Where(e => e.Score == 300).Should().OnlyContain(e => e.Rank == 1);
        result.Where(e => e.Score == 200).Should().OnlyContain(e => e.Rank == 3);
        result.Where(e => e.Score == 100).Should().OnlyContain(e => e.Rank == 5);
    }

    [Fact]
    public async Task GetEntitiesByRankRangeAsync_ModifiedCompetition_TiedPlayersShareRankWithGapsBefore()
    {
        // Arrange
        await SeedAsync([("p1", 300), ("p2", 300), ("p3", 200), ("p4", 200), ("p5", 100)]);

        // Act
        var result = await Leaderboard.GetEntitiesByRankRangeAsync(Key, 1, 5, RankingType.ModifiedCompetition);

        // Assert
        result.Should().HaveCount(5);
        result.Where(e => e.Score == 300).Should().OnlyContain(e => e.Rank == 2);
        result.Where(e => e.Score == 200).Should().OnlyContain(e => e.Rank == 4);
        result.Where(e => e.Score == 100).Should().OnlyContain(e => e.Rank == 5);
    }

    [Fact]
    public async Task GetEntitiesByRankRangeAsync_RanksMatchGetEntityRankAsync()
    {
        // Arrange
        await SeedAsync([("A", 500), ("B", 400), ("C", 300), ("D", 200), ("E", 100)]);

        // Act
        var rangeResult = await Leaderboard.GetEntitiesByRankRangeAsync(Key, 1, 5);

        // Assert
        foreach (var player in rangeResult)
        {
            var individualRank = await Leaderboard.GetEntityRankAsync(Key, player.Id);
            player.Rank.Should().Be(individualRank!.Value,
                because: $"rank of {player.Id} from range query should match individual rank query");
        }
    }
}
