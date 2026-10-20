using System.Globalization;
using Redisboard.NET.Common.Models;
using Redisboard.NET.Enumerations;

namespace Redisboard.NET.Tests.Integration;

/// <summary>Verifies numeric input and Redis result boundaries through the public API.</summary>
public class NumericBoundaryTests : LeaderboardTestBase
{
    /// <summary>Initializes tests with shared Redis infrastructure and isolated keys.</summary>
    /// <param name="fixture">Provides the Redis-backed leaderboard.</param>
    public NumericBoundaryTests(LeaderboardFixture fixture) : base(fixture) { }

    /// <summary>Returns ranking modes for numeric read regressions.</summary>
    public static TheoryData<RankingType> RankingModes => new()
    {
        RankingType.Default, RankingType.DenseRank,
        RankingType.StandardCompetition, RankingType.ModifiedCompetition
    };

    /// <summary>Returns rejected scores and write acknowledgment options.</summary>
    public static TheoryData<double, bool> InvalidScores => new()
    {
        { double.NaN, false }, { double.PositiveInfinity, false }, { double.NegativeInfinity, false },
        { double.NaN, true }, { double.PositiveInfinity, true }, { double.NegativeInfinity, true }
    };

    /// <summary>Verifies fractional Redis scores remain independent of the caller's culture.</summary>
    /// <param name="rankingType">Ranking mode used for the read.</param>
    [Theory]
    [MemberData(nameof(RankingModes))]
    public async Task RankRange_CommaDecimalCulture_PreservesScores(RankingType rankingType)
    {
        await SeedAsync([("A", 99.5), ("B", 42.25)]);
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            foreach (var culture in new[] { "de-DE", "fr-FR", "sv-SE", "bg-BG" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);

                var result = await Leaderboard.GetEntitiesByRankRangeAsync(Key, 1, 2, rankingType);

                Assert.Equal(new[] { 99.5, 42.25 }, result.Select(p => p.Score));
                Assert.Equal(new long[] { 1, 2 }, result.Select(p => p.Rank));
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    /// <summary>Verifies very large inclusive rank endpoints preserve all matching rows.</summary>
    /// <param name="rankingType">Ranking mode used for the read.</param>
    [Theory]
    [MemberData(nameof(RankingModes))]
    public async Task RankRange_LongMaxValueEnd_ReturnsAllRemainingEntities(RankingType rankingType)
    {
        await SeedAsync([("A", 300), ("B", 200), ("C", 100)]);

        var result = await Leaderboard.GetEntitiesByRankRangeAsync(Key, 2, long.MaxValue, rankingType);

        Assert.Equal(new[] { "B", "C" }, result.Select(p => p.Id));
    }

    /// <summary>Verifies very large start indexes remain out of range instead of wrapping.</summary>
    /// <param name="rankingType">Ranking mode used for the read.</param>
    [Theory]
    [MemberData(nameof(RankingModes))]
    public async Task RankRange_LongMaxValueStart_ReturnsEmpty(RankingType rankingType)
    {
        await SeedAsync([("A", 300), ("B", 200)]);

        var result = await Leaderboard.GetEntitiesByRankRangeAsync(Key, long.MaxValue, long.MaxValue, rankingType);

        Assert.Empty(result);
    }

    /// <summary>Verifies large neighbor windows include the target and all available neighbors.</summary>
    /// <param name="rankingType">Ranking mode used for the read.</param>
    [Theory]
    [MemberData(nameof(RankingModes))]
    public async Task Neighbours_IntMaxValueOffset_ReturnsAllEntities(RankingType rankingType)
    {
        await SeedAsync([("A", 300), ("B", 200), ("C", 100)]);

        var result = await Leaderboard.GetEntityAndNeighboursAsync(Key, "B", int.MaxValue, rankingType);

        Assert.Equal(new[] { "A", "B", "C" }, result.Select(p => p.Id));
    }

    /// <summary>Verifies invalid batch scores fail before any entity reaches Redis.</summary>
    /// <param name="score">Nonfinite score to reject.</param>
    /// <param name="fireAndForget">Whether the write requests acknowledgment.</param>
    [Theory]
    [MemberData(nameof(InvalidScores))]
    public async Task AddBatch_NonfiniteScore_DoesNotPersistAnyEntries(double score, bool fireAndForget)
    {
        var entries = new[]
        {
            new Player { Id = "A", Score = 10 },
            new Player { Id = "B", Score = score },
            new Player { Id = "C", Score = 20 }
        };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Leaderboard.AddEntitiesAsync(Key, entries, fireAndForget));

        Assert.Equal(0, await Leaderboard.GetSizeAsync(Key));
        Assert.Empty(await Leaderboard.GetEntitiesByRankRangeAsync(Key, 1, 10));
        await Leaderboard.AddEntityAsync(Key, new Player { Id = "valid", Score = 5 });
        Assert.Equal(1, await Leaderboard.GetEntityRankAsync(Key, "valid", RankingType.DenseRank));
    }

    /// <summary>Verifies single-entity adds reject nonfinite scores before storage.</summary>
    /// <param name="score">Nonfinite score to reject.</param>
    /// <param name="fireAndForget">Whether the write requests acknowledgment.</param>
    [Theory]
    [MemberData(nameof(InvalidScores))]
    public async Task AddEntity_NonfiniteScore_DoesNotPersistEntity(double score, bool fireAndForget)
    {
        var entity = new Player { Id = "A", Score = score };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Leaderboard.AddEntityAsync(Key, entity, fireAndForget));

        Assert.Null(await Leaderboard.GetEntityScoreAsync(Key, "A"));
    }

    /// <summary>Verifies both score-update overloads preserve existing scores on invalid input.</summary>
    /// <param name="score">Nonfinite score to reject.</param>
    /// <param name="fireAndForget">Whether the write requests acknowledgment.</param>
    [Theory]
    [MemberData(nameof(InvalidScores))]
    public async Task UpdateScore_NonfiniteScore_PreservesExistingScore(double score, bool fireAndForget)
    {
        await SeedAsync([("A", 10)]);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Leaderboard.UpdateEntityScoreAsync(Key, "A", score, fireAndForget));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Leaderboard.UpdateEntityScoreAsync(Key, new Player { Id = "A", Score = score }, fireAndForget));

        Assert.Equal(10, await Leaderboard.GetEntityScoreAsync(Key, "A"));
    }
    /// <summary>Verifies negative scores survive persistence and ranking reads.</summary>
    /// <param name="rankingType">Ranking mode used for the read.</param>
    [Theory]
    [MemberData(nameof(RankingModes))]
    public async Task RankRange_SignedScores_PreservesSignAndDescendingOrder(RankingType rankingType)
    {
        await SeedAsync([("A", 5), ("B", 0), ("C", -5)]);

        var result = await Leaderboard.GetEntitiesByRankRangeAsync(Key, 1, 3, rankingType);

        Assert.Equal(new[] { 5d, 0d, -5d }, result.Select(p => p.Score));
        Assert.Equal(-5, await Leaderboard.GetEntityScoreAsync(Key, "C"));
    }

}
