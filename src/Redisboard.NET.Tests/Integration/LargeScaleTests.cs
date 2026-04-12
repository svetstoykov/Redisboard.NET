using FluentAssertions;
using Redisboard.NET.Enumerations;

namespace Redisboard.NET.Tests.Integration;

/// <summary>
/// Large-scale ranking correctness tests. Verifies that ranking strategies produce
/// correct results with thousands of entries, including three-group tie scenarios
/// (3 x 1 000), 10 000 unique-score offset arithmetic, and randomized page-size tests.
/// </summary>
public class LargeScaleTests : LeaderboardTestBase
{
    private const int GroupSize = 1_000;
    private const int DeterministicOffset = 17;
    private const int DeterministicPlayerCount = 144;
    private const int DeterministicPlayerIndex = 72;

    public LargeScaleTests(LeaderboardFixture fixture) : base(fixture) { }

    private static IEnumerable<(string key, double score)> ThreeGroupPlayers()
    {
        for (var i = 0; i < GroupSize; i++) yield return ($"a{i}", 3_000);
        for (var i = 0; i < GroupSize; i++) yield return ($"b{i}", 2_000);
        for (var i = 0; i < GroupSize; i++) yield return ($"c{i}", 1_000);
    }

    [Fact]
    public async Task LargeDataset_DefaultRanking_GroupRangesAreCorrect()
    {
        // Arrange
        await SeedBulkAsync(ThreeGroupPlayers());

        // Act
        var result = await Leaderboard.GetEntityAndNeighboursAsync(
            Key, "a0", offset: GroupSize + 1, RankingType.Default);

        // Assert
        result.Where(e => e.Id.StartsWith("a"))
            .Should().OnlyContain(e => e.Rank >= 1 && e.Rank <= GroupSize);

        result.Where(e => e.Id.StartsWith("b"))
            .Should().OnlyContain(e => e.Rank > GroupSize && e.Rank <= GroupSize * 2);
    }

    [Fact]
    public async Task LargeDataset_DenseRanking_AllGroupsHaveCorrectRank()
    {
        // Arrange
        await SeedBulkAsync(ThreeGroupPlayers());

        // Act
        var result = await Leaderboard.GetEntityAndNeighboursAsync(
            Key, "b0", offset: GroupSize + 1, RankingType.DenseRank);

        // Assert
        result.Where(e => e.Id.StartsWith("a"))
            .Should().OnlyContain(e => e.Rank == 1);

        result.Where(e => e.Id.StartsWith("b"))
            .Should().OnlyContain(e => e.Rank == 2);

        result.Where(e => e.Id.StartsWith("c"))
            .Should().OnlyContain(e => e.Rank == 3);
    }

    [Fact]
    public async Task LargeDataset_StandardCompetition_AllGroupsHaveCorrectRank()
    {
        // Arrange
        await SeedBulkAsync(ThreeGroupPlayers());

        // Act
        var result = await Leaderboard.GetEntityAndNeighboursAsync(
            Key, "b0", offset: GroupSize + 1, RankingType.StandardCompetition);

        // Assert
        result.Where(e => e.Id.StartsWith("a"))
            .Should().OnlyContain(e => e.Rank == 1);

        result.Where(e => e.Id.StartsWith("b"))
            .Should().OnlyContain(e => e.Rank == GroupSize + 1);

        result.Where(e => e.Id.StartsWith("c"))
            .Should().OnlyContain(e => e.Rank == GroupSize * 2 + 1);
    }

    [Fact]
    public async Task LargeDataset_ModifiedCompetition_AllGroupsHaveCorrectRank()
    {
        // Arrange
        await SeedBulkAsync(ThreeGroupPlayers());

        // Act
        var result = await Leaderboard.GetEntityAndNeighboursAsync(
            Key, "b0", offset: GroupSize + 1, RankingType.ModifiedCompetition);

        // Assert
        result.Where(e => e.Id.StartsWith("a"))
            .Should().OnlyContain(e => e.Rank == GroupSize);

        result.Where(e => e.Id.StartsWith("b"))
            .Should().OnlyContain(e => e.Rank == GroupSize * 2);

        result.Where(e => e.Id.StartsWith("c"))
            .Should().OnlyContain(e => e.Rank == GroupSize * 3);
    }

    [Theory]
    [InlineData(RankingType.Default)]
    [InlineData(RankingType.DenseRank)]
    [InlineData(RankingType.StandardCompetition)]
    [InlineData(RankingType.ModifiedCompetition)]
    public async Task UniqueScores_10k_MiddlePlayer_CorrectOffsetCount(RankingType rankingType)
    {
        const int total = 10_000;
        const int offset = 50;
        const int targetIndex = 5_000;

        // Arrange
        await SeedBulkAsync(
            Enumerable.Range(0, total).Select(i => ($"p10k_{i}", (double)i)),
            batchSize: 1_000);

        // Act
        var result = await Leaderboard.GetEntityAndNeighboursAsync(
            Key, $"p10k_{targetIndex}", offset, rankingType);

        // Assert
        result.Should().HaveCount(offset * 2 + 1);
        result.Should().OnlyContain(r => r.Id.StartsWith("p10k_"));
        result.Should().OnlyContain(r => r.Score > 0);
    }

    [Theory]
    [InlineData(RankingType.Default)]
    [InlineData(RankingType.DenseRank)]
    [InlineData(RankingType.StandardCompetition)]
    [InlineData(RankingType.ModifiedCompetition)]
    public async Task DeterministicPageSize_MiddlePlayer_ReturnsFullWindow(RankingType rankingType)
    {
        // Arrange
        await SeedBulkAsync(LeaderboardSeed.Sequence("player", 1, DeterministicPlayerCount));

        // Act
        var result = await Leaderboard.GetEntityAndNeighboursAsync(
            Key,
            $"player{DeterministicPlayerIndex}",
            DeterministicOffset,
            rankingType);

        // Assert
        result.Should().HaveCount(DeterministicOffset * 2 + 1);
        result.Should().ContainSingle(player => player.Id == $"player{DeterministicPlayerIndex}");
    }
}
