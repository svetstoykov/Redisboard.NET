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

    public LargeScaleTests(LeaderboardFixture fixture) : base(fixture) { }

    // ------------------------------------------------------------------ //
    // Helpers
    // ------------------------------------------------------------------ //

    private static IEnumerable<(string key, double score)> ThreeGroupPlayers()
    {
        for (var i = 0; i < GroupSize; i++) yield return ($"a{i}", 3_000);
        for (var i = 0; i < GroupSize; i++) yield return ($"b{i}", 2_000);
        for (var i = 0; i < GroupSize; i++) yield return ($"c{i}", 1_000);
    }

    // ------------------------------------------------------------------ //
    // Three-group (3 x 1 000) ranking correctness
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task LargeDataset_DefaultRanking_GroupRangesAreCorrect()
    {
        await SeedAsync(ThreeGroupPlayers());

        var result = await Leaderboard.GetEntityAndNeighboursAsync(
            Key, "a0", offset: GroupSize + 1, RankingType.Default);

        result.Where(e => ((string)e.Key!).StartsWith("a"))
            .Should().OnlyContain(e => e.Rank >= 1 && e.Rank <= GroupSize);

        result.Where(e => ((string)e.Key!).StartsWith("b"))
            .Should().OnlyContain(e => e.Rank > GroupSize && e.Rank <= GroupSize * 2);
    }

    [Fact]
    public async Task LargeDataset_DenseRanking_AllGroupsHaveCorrectRank()
    {
        await SeedAsync(ThreeGroupPlayers());

        var result = await Leaderboard.GetEntityAndNeighboursAsync(
            Key, "b0", offset: GroupSize + 1, RankingType.DenseRank);

        result.Where(e => ((string)e.Key!).StartsWith("a"))
            .Should().OnlyContain(e => e.Rank == 1);

        result.Where(e => ((string)e.Key!).StartsWith("b"))
            .Should().OnlyContain(e => e.Rank == 2);

        result.Where(e => ((string)e.Key!).StartsWith("c"))
            .Should().OnlyContain(e => e.Rank == 3);
    }

    [Fact]
    public async Task LargeDataset_StandardCompetition_AllGroupsHaveCorrectRank()
    {
        await SeedAsync(ThreeGroupPlayers());

        var result = await Leaderboard.GetEntityAndNeighboursAsync(
            Key, "b0", offset: GroupSize + 1, RankingType.StandardCompetition);

        result.Where(e => ((string)e.Key!).StartsWith("a"))
            .Should().OnlyContain(e => e.Rank == 1);

        result.Where(e => ((string)e.Key!).StartsWith("b"))
            .Should().OnlyContain(e => e.Rank == GroupSize + 1);

        result.Where(e => ((string)e.Key!).StartsWith("c"))
            .Should().OnlyContain(e => e.Rank == GroupSize * 2 + 1);
    }

    [Fact]
    public async Task LargeDataset_ModifiedCompetition_AllGroupsHaveCorrectRank()
    {
        await SeedAsync(ThreeGroupPlayers());

        var result = await Leaderboard.GetEntityAndNeighboursAsync(
            Key, "b0", offset: GroupSize + 1, RankingType.ModifiedCompetition);

        result.Where(e => ((string)e.Key!).StartsWith("a"))
            .Should().OnlyContain(e => e.Rank == GroupSize);

        result.Where(e => ((string)e.Key!).StartsWith("b"))
            .Should().OnlyContain(e => e.Rank == GroupSize * 2);

        result.Where(e => ((string)e.Key!).StartsWith("c"))
            .Should().OnlyContain(e => e.Rank == GroupSize * 3);
    }

    // ------------------------------------------------------------------ //
    // 10 000 unique-score offset arithmetic
    // ------------------------------------------------------------------ //

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

        await SeedAsync(
            Enumerable.Range(0, total).Select(i => ($"p10k_{i}", (double)i)),
            concurrency: 100);

        var result = await Leaderboard.GetEntityAndNeighboursAsync(
            Key, $"p10k_{targetIndex}", offset, rankingType);

        result.Should().HaveCount(offset * 2 + 1);
        result.Should().OnlyContain(r => ((string)r.Key!).StartsWith("p10k_"));
        result.Should().OnlyContain(r => r.Score > 0);
    }

    // ------------------------------------------------------------------ //
    // Randomized page-size correctness
    // ------------------------------------------------------------------ //

    [Theory]
    [InlineData(RankingType.Default)]
    [InlineData(RankingType.DenseRank)]
    [InlineData(RankingType.StandardCompetition)]
    [InlineData(RankingType.ModifiedCompetition)]
    public async Task RandomizedPageSize_MiddlePlayer_CorrectCount(RankingType rankingType)
    {
        const int maxPlayersCount = 500;
        const int minOffset = 10;
        const int maxOffset = 20;

        var random = new Random();
        var offset = random.Next(minOffset, maxOffset);
        var playersCount = random.Next(offset + 1, maxPlayersCount);

        await SeedAsync(
            Enumerable.Range(0, playersCount).Select(i => ($"player{i}", (double)i)));

        var playerIndex = random.Next(offset + 1, playersCount - offset);
        var playerKey = $"player{playerIndex}";

        var result = await Leaderboard.GetEntityAndNeighboursAsync(Key, playerKey, offset, rankingType);

        result.Should().HaveCount(offset * 2 + 1);
        result.Should().OnlyContain(r => r.Score > 0);
    }
}
