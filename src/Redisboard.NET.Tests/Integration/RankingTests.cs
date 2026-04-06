using FluentAssertions;
using Redisboard.NET.Enumerations;

namespace Redisboard.NET.Tests.Integration;

/// <summary>
/// Tests for <see cref="Leaderboard.GetEntityAndNeighboursAsync"/> and
/// <see cref="Leaderboard.GetEntityRankAsync"/> across all ranking types.
/// Covers rank-value correctness, offset counts for first/middle positions,
/// and multi-update rank transitions.
/// </summary>
public class RankingTests : LeaderboardTestBase
{
    public RankingTests(LeaderboardFixture fixture) : base(fixture) { }

    // GetEntityAndNeighboursAsync — rank-value assertions

    [Fact]
    public async Task GetEntityAndNeighboursAsync_DefaultRanking_ReturnsCorrectRanks()
    {
        await SeedAsync([("Mike", 200), ("Alex", 100), ("John", 100), ("Sam", 50)]);

        var result = await Leaderboard.GetEntityAndNeighboursAsync(Key, "Alex", offset: 10);

        result.Should().HaveCount(4);
        result.First(p => p.Key == "Mike").Rank.Should().Be(1);
        result.First(p => p.Key == "Alex").Rank.Should().Be(2);
        result.First(p => p.Key == "John").Rank.Should().Be(3);
        result.First(p => p.Key == "Sam").Rank.Should().Be(4);
    }

    [Fact]
    public async Task GetEntityAndNeighboursAsync_DenseRanking_ReturnsCorrectRanks()
    {
        await SeedAsync([("player1", 250), ("player2", 200), ("player3", 100), ("player4", 100), ("player5", 50)]);

        var result = await Leaderboard.GetEntityAndNeighboursAsync(
            Key, "player2", offset: 10, RankingType.DenseRank);

        result.Should().HaveCount(5);
        result.First(p => p.Key == "player1").Rank.Should().Be(1);
        result.First(p => p.Key == "player2").Rank.Should().Be(2);
        result.First(p => p.Key == "player3").Rank.Should().Be(3);
        result.First(p => p.Key == "player4").Rank.Should().Be(3);
        result.First(p => p.Key == "player5").Rank.Should().Be(4);
    }

    [Fact]
    public async Task GetEntityAndNeighboursAsync_StandardCompetition_ReturnsCorrectRanks()
    {
        await SeedAsync([("player1", 250), ("player2", 200), ("player3", 100), ("player4", 100), ("player5", 50)]);

        var result = await Leaderboard.GetEntityAndNeighboursAsync(
            Key, "player2", offset: 10, RankingType.StandardCompetition);

        result.Should().HaveCount(5);
        result.First(p => p.Key == "player1").Rank.Should().Be(1);
        result.First(p => p.Key == "player2").Rank.Should().Be(2);
        result.First(p => p.Key == "player3").Rank.Should().Be(3);
        result.First(p => p.Key == "player4").Rank.Should().Be(3);
        result.First(p => p.Key == "player5").Rank.Should().Be(5);
    }

    [Fact]
    public async Task GetEntityAndNeighboursAsync_ModifiedCompetition_ReturnsCorrectRanks()
    {
        await SeedAsync([("player1", 250), ("player2", 200), ("player3", 100), ("player4", 100), ("player5", 50)]);

        var result = await Leaderboard.GetEntityAndNeighboursAsync(
            Key, "player2", offset: 10, RankingType.ModifiedCompetition);

        result.Should().HaveCount(5);
        result.First(p => p.Key == "player1").Rank.Should().Be(1);
        result.First(p => p.Key == "player2").Rank.Should().Be(2);
        result.First(p => p.Key == "player3").Rank.Should().Be(4);
        result.First(p => p.Key == "player4").Rank.Should().Be(4);
        result.First(p => p.Key == "player5").Rank.Should().Be(5);
    }

    // GetEntityAndNeighboursAsync — offset count for middle player

    [Theory]
    [InlineData(RankingType.Default)]
    [InlineData(RankingType.DenseRank)]
    [InlineData(RankingType.StandardCompetition)]
    [InlineData(RankingType.ModifiedCompetition)]
    public async Task GetEntityAndNeighboursAsync_MiddlePlayer_ReturnsCorrectOffsetCount(RankingType rankingType)
    {
        const int offset = 2;
        await SeedAsync([
            ("Mike", 200), ("Alex", 100), ("John", 100),
            ("Sam", 55), ("Jim", 50), ("Dodo", 40), ("Frodo", 30)
        ]);

        var result = await Leaderboard.GetEntityAndNeighboursAsync(Key, "John", offset: offset, rankingType);

        result.Should().HaveCount(offset * 2 + 1);
    }

    // GetEntityAndNeighboursAsync — offset count for first-place player

    [Theory]
    [InlineData(RankingType.Default)]
    [InlineData(RankingType.DenseRank)]
    [InlineData(RankingType.StandardCompetition)]
    [InlineData(RankingType.ModifiedCompetition)]
    public async Task GetEntityAndNeighboursAsync_FirstPlace_ReturnsClampedOffset(RankingType rankingType)
    {
        const int offset = 2;
        await SeedAsync([
            ("Mike", 200), ("Alex", 100), ("John", 100),
            ("Sam", 55), ("Jim", 50), ("Dodo", 40), ("Frodo", 30)
        ]);

        var result = await Leaderboard.GetEntityAndNeighboursAsync(Key, "Mike", offset: offset, rankingType);

        result.Should().HaveCount(offset + 1);
    }

    // GetEntityRankAsync — standalone rank queries

    [Fact]
    public async Task GetEntityRankAsync_DefaultRanking_ReturnsCorrectRank()
    {
        await SeedAsync([("Mike", 200), ("Alex", 100), ("John", 100), ("Sam", 50)]);

        var result = await Leaderboard.GetEntityRankAsync(Key, "John");

        result.Should().Be(3);
    }

    [Fact]
    public async Task GetEntityRankAsync_DenseRank_ReturnsCorrectRank()
    {
        await SeedAsync([("player1", 250), ("player2", 200), ("player3", 100), ("player4", 100), ("player5", 50)]);

        var result = await Leaderboard.GetEntityRankAsync(Key, "player5", RankingType.DenseRank);

        result.Should().Be(4);
    }

    [Fact]
    public async Task GetEntityRankAsync_StandardCompetition_ReturnsCorrectRank()
    {
        await SeedAsync([("player1", 250), ("player2", 200), ("player3", 100), ("player4", 100), ("player5", 50)]);

        var result = await Leaderboard.GetEntityRankAsync(Key, "player4", RankingType.StandardCompetition);

        result.Should().Be(3);
    }

    [Fact]
    public async Task GetEntityRankAsync_ModifiedCompetition_ReturnsCorrectRank()
    {
        await SeedAsync([("player1", 250), ("player2", 200), ("player3", 100), ("player4", 100), ("player5", 50)]);

        var result = await Leaderboard.GetEntityRankAsync(Key, "player4", RankingType.ModifiedCompetition);

        result.Should().Be(4);
    }

    [Fact]
    public async Task GetEntityRankAsync_DenseRank_AfterMultipleScoreUpdates_ReturnsCorrectRank()
    {
        await SeedAsync([("player1", 250), ("player2", 200), ("player3", 100), ("player4", 100), ("player5", 50)]);

        await Leaderboard.UpdateEntityScoreAsync(Key, "player5", 40);

        var result = await Leaderboard.GetEntityRankAsync(Key, "player5", RankingType.DenseRank);

        result.Should().Be(4);
    }
}
