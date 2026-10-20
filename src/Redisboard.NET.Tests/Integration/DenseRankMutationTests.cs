using Redisboard.NET.Common.Models;
using Redisboard.NET.Enumerations;
using StackExchange.Redis;

namespace Redisboard.NET.Tests.Integration;

/// <summary>Verifies leaderboard mutations retain exactly the score groups needed by dense ranking.</summary>
/// <remarks>
/// Uses public operations and independent expected ranks against real Redis. Each test owns its
/// leaderboard key through <see cref="LeaderboardTestBase"/> so mutation history cannot leak between tests.
/// </remarks>
public class DenseRankMutationTests : LeaderboardTestBase
{
    /// <summary>Initializes mutation regressions using the shared Redis fixture.</summary>
    /// <param name="fixture">Provides the Redis-backed leaderboard used by each isolated test.</param>
    public DenseRankMutationTests(LeaderboardFixture fixture) : base(fixture) { }

    /// <summary>Verifies replacing an entity's score removes obsolete score groups.</summary>
    /// <param name="score">Replacement score above or below the other entity.</param>
    /// <param name="playerRank">Expected dense rank of the replaced entity.</param>
    /// <param name="otherRank">Expected dense rank of the unchanged entity.</param>
    /// <returns>A task that completes after checking ranks, score, and replacement metadata.</returns>
    [Theory]
    [InlineData(200d, 1L, 2L)]
    [InlineData(25d, 2L, 1L)]
    public async Task AddEntityAsync_ExistingKey_RemovesUnusedPreviousScore(double score, long playerRank, long otherRank)
    {
        using var culture = new InvariantCultureScope();
        await SeedBulkAsync([("p1", 100), ("p2", 50)]);

        await Leaderboard.AddEntityAsync(Key, new Player { Id = "p1", Score = score, Username = "updated" });

        var result = await Leaderboard.GetEntitiesByRankRangeAsync(Key, 1, 3, RankingType.DenseRank);
        Assert.Equal(2, result.Length);
        var player = Assert.Single(result, entity => entity.Id == "p1");
        Assert.Equal(playerRank, player.Rank);
        Assert.Equal(score, player.Score);
        Assert.Equal("updated", player.Username);
        Assert.Equal(otherRank, Assert.Single(result, entity => entity.Id == "p2").Rank);
    }

    /// <summary>Verifies batch upserts remove all score groups abandoned by the replaced entities.</summary>
    /// <returns>A task that completes after checking the remaining dense ranks.</returns>
    [Fact]
    public async Task AddEntitiesAsync_ExistingKeys_RemovesUnusedPreviousScores()
    {
        using var culture = new InvariantCultureScope();
        await SeedBulkAsync([("p1", 100), ("p2", 50), ("bottom", 0)]);

        await Leaderboard.AddEntitiesAsync(Key,
            [new Player { Id = "p1", Score = 200 }, new Player { Id = "p2", Score = 300 }]);

        await AssertRanksAsync(("p2", 1), ("p1", 2), ("bottom", 3));
    }

    /// <summary>Verifies an upsert retains the previous score while another entity still holds it.</summary>
    /// <returns>A task that completes after checking the retained score group's dense rank.</returns>
    [Fact]
    public async Task AddEntityAsync_SharedPreviousScore_PreservesRemainingScoreGroup()
    {
        using var culture = new InvariantCultureScope();
        await SeedBulkAsync([("p1", 100), ("p2", 100), ("bottom", 50)]);

        await Leaderboard.AddEntityAsync(Key, new Player { Id = "p1", Score = 200 });

        await AssertRanksAsync(("p1", 1), ("p2", 2), ("bottom", 3));
    }

    /// <summary>Verifies a same-score upsert preserves ties while replacing metadata.</summary>
    /// <returns>A task that completes after checking shared ranks and replacement metadata.</returns>
    [Fact]
    public async Task AddEntityAsync_UnchangedScore_PreservesTiesAndReplacesMetadata()
    {
        using var culture = new InvariantCultureScope();
        await SeedBulkAsync([("p1", 100), ("p2", 100), ("bottom", 50)]);

        await Leaderboard.AddEntityAsync(Key, new Player { Id = "p1", Score = 100, Username = "updated" });

        var result = await AssertRanksAsync(("p1", 1), ("p2", 1), ("bottom", 2));
        Assert.Equal("updated", result[0].Username);
    }

    /// <summary>Verifies the last duplicate key in a batch supplies the final score and metadata.</summary>
    /// <returns>A task that completes after checking the final entity and gapless dense ranks.</returns>
    [Fact]
    public async Task AddEntitiesAsync_DuplicateKey_LastEntryWinsWithoutLeavingScoreGroups()
    {
        using var culture = new InvariantCultureScope();
        await SeedBulkAsync([("p2", 50)]);

        await Leaderboard.AddEntitiesAsync(Key,
        [
            new Player { Id = "p1", Score = 100, Username = "first" },
            new Player { Id = "p1", Score = 200, Username = "last" }
        ]);

        var result = await AssertRanksAsync(("p1", 1), ("p2", 2));
        Assert.Equal(200d, result[0].Score);
        Assert.Equal("last", result[0].Username);
    }

    /// <summary>Verifies a batch can move entities between each other's score groups.</summary>
    /// <returns>A task that completes after checking dense ranks after the swap.</returns>
    [Fact]
    public async Task AddEntitiesAsync_SwappedScores_PreservesBothScoreGroups()
    {
        using var culture = new InvariantCultureScope();
        await SeedBulkAsync([("p1", 100), ("p2", 50), ("bottom", 0)]);

        await Leaderboard.AddEntitiesAsync(Key,
            [new Player { Id = "p1", Score = 50 }, new Player { Id = "p2", Score = 100 }]);

        await AssertRanksAsync(("p2", 1), ("p1", 2), ("bottom", 3));
    }

    /// <summary>Verifies score updates preserve precise values and usable dense-rank entries.</summary>
    /// <param name="score">Replacement score covering Redis's finite double range.</param>
    /// <returns>A task that completes after checking the updated score and rank.</returns>
    [Theory]
    [MemberData(nameof(DenseRankPrecisionTests.PrecisionSensitiveScores), MemberType = typeof(DenseRankPrecisionTests))]
    public async Task UpdateEntityScoreAsync_PrecisionSensitiveScore_ReturnsExactScoreAndRank(double score)
    {
        using var culture = new InvariantCultureScope();
        await SeedBulkAsync([("p1", 1), ("bottom", 0)]);

        await Leaderboard.UpdateEntityScoreAsync(Key, "p1", score);

        var result = await Leaderboard.GetEntityAndNeighboursAsync(Key, "p1", 1, RankingType.DenseRank);
        var player = Assert.Single(result, entity => entity.Id == "p1");
        Assert.Equal(score, player.Score);
        Assert.Equal(1L, player.Rank);
    }

    /// <summary>Verifies a score update cannot merge adjacent doubles into one score group.</summary>
    /// <returns>A task that completes after checking the distinct dense ranks and updated score.</returns>
    [Fact]
    public async Task UpdateEntityScoreAsync_AdjacentScores_KeepsDistinctDenseRanks()
    {
        using var culture = new InvariantCultureScope();
        await SeedBulkAsync([("p1", 1), ("p2", 0.3), ("bottom", 0)]);
        var higherScore = Math.BitIncrement(0.3);

        await Leaderboard.UpdateEntityScoreAsync(Key, "p1", higherScore);

        var result = await AssertRanksAsync(("p1", 1), ("p2", 2), ("bottom", 3));
        Assert.Equal(higherScore, result[0].Score);
        Assert.Equal(0.3, result[1].Score);
    }

    /// <summary>Verifies repeated updates to the same score preserve existing ties.</summary>
    /// <param name="score">Score shared by the updated entities.</param>
    /// <param name="bottomRank">Expected rank of the zero-score entity.</param>
    /// <returns>A task that completes after checking stable dense ranks.</returns>
    [Theory]
    [InlineData(0d, 1L)]
    [InlineData(0.1 + 0.2, 2L)]
    [InlineData(1e14, 2L)]
    public async Task UpdateEntityScoreAsync_UnchangedScore_PreservesTies(double score, long bottomRank)
    {
        using var culture = new InvariantCultureScope();
        await SeedBulkAsync([("p1", score), ("p2", score), ("z-bottom", 0)]);

        await Leaderboard.UpdateEntityScoreAsync(Key, "p1", score);

        await AssertRanksAsync(("p1", 1), ("p2", 1), ("z-bottom", bottomRank));
    }

    /// <summary>Verifies updating away from a computed fraction removes its abandoned score group.</summary>
    /// <returns>A task that completes after checking gapless dense ranks.</returns>
    [Fact]
    public async Task UpdateEntityScoreAsync_MultipleChanges_RemovesObsoleteFractionalScore()
    {
        using var culture = new InvariantCultureScope();
        await SeedBulkAsync([("p1", 1.5), ("bottom", 0)]);
        await Leaderboard.UpdateEntityScoreAsync(Key, "p1", 0.1 + 0.2);

        await Leaderboard.UpdateEntityScoreAsync(Key, "p1", 7);

        await AssertRanksAsync(("p1", 1), ("bottom", 2));
    }

    /// <summary>Verifies deletion removes the score group when its last holder leaves.</summary>
    /// <param name="score">Score whose index entry must disappear when no entity holds it.</param>
    /// <returns>A task that completes after checking the survivor's dense rank.</returns>
    [Theory]
    [MemberData(nameof(DenseRankPrecisionTests.PrecisionSensitiveScores), MemberType = typeof(DenseRankPrecisionTests))]
    public async Task DeleteEntityAsync_PrecisionSensitiveScore_RemovesUnusedScoreGroup(double score)
    {
        using var culture = new InvariantCultureScope();
        await SeedBulkAsync([("removed", score), ("bottom", 0)]);

        await Leaderboard.DeleteEntityAsync(Key, "removed");

        await AssertRanksAsync(("bottom", 1));
    }

    /// <summary>Verifies duplicate deletions cannot remove a score still held by another entity.</summary>
    /// <returns>A task that completes after checking the retained fractional score group.</returns>
    [Fact]
    public async Task DeleteEntitiesAsync_OneTiedEntity_PreservesSharedScoreGroup()
    {
        using var culture = new InvariantCultureScope();
        await SeedBulkAsync([("p1", 0.1 + 0.2), ("p2", 0.1 + 0.2), ("bottom", 0)]);

        await Leaderboard.DeleteEntitiesAsync(Key, ["p1", "p1", "missing"]);

        await AssertRanksAsync(("p2", 1), ("bottom", 2));
    }

    /// <summary>Verifies deleting all tied entities removes their fractional score group once.</summary>
    /// <returns>A task that completes after checking the remaining dense rank.</returns>
    [Fact]
    public async Task DeleteEntitiesAsync_LastTiedEntities_RemovesSharedScoreGroup()
    {
        using var culture = new InvariantCultureScope();
        await SeedBulkAsync([("p1", 0.1 + 0.2), ("p2", 0.1 + 0.2), ("bottom", 0)]);

        await Leaderboard.DeleteEntitiesAsync(Key, ["p1", "p2", "p1", "missing"]);

        await AssertRanksAsync(("bottom", 1));
    }

    /// <summary>Verifies mixed mutations agree with an independent mathematical dense-rank model.</summary>
    /// <returns>A task that completes after checking every intermediate leaderboard state.</returns>
    [Fact]
    public async Task MutationSequence_DenseRanksMatchReferenceModel()
    {
        using var culture = new InvariantCultureScope();
        var expectedScores = new Dictionary<string, double>();

        async Task AddAsync(string id, double score)
        {
            await Leaderboard.AddEntityAsync(Key, new Player { Id = id, Score = score });
            expectedScores[id] = score;
        }

        async Task AddBatchAsync(params (string Id, double Score)[] entries)
        {
            await Leaderboard.AddEntitiesAsync(Key, entries.Select(entry => new Player { Id = entry.Id, Score = entry.Score }));
            foreach (var entry in entries)
                expectedScores[entry.Id] = entry.Score;
        }

        async Task UpdateAsync(string id, double score)
        {
            await Leaderboard.UpdateEntityScoreAsync(Key, id, score);
            expectedScores[id] = score;
        }

        async Task DeleteAsync(params string[] ids)
        {
            await Leaderboard.DeleteEntitiesAsync(Key, ids.Select(id => (RedisValue)id));
            foreach (var id in ids)
                expectedScores.Remove(id);
        }

        Func<Task>[] mutations =
        [
            () => AddBatchAsync(("p1", 100), ("p2", 50), ("p3", 0.3), ("p4", 0.1 + 0.2), ("p5", 0)),
            () => AddAsync("p1", 200),
            () => AddAsync("p1", 0.3),
            () => AddBatchAsync(("p2", 400), ("p2", 50)),
            () => UpdateAsync("p3", 1e15),
            () => UpdateAsync("p3", 0.1 + 0.2),
            () => AddAsync("p1", 0.3),
            () => DeleteAsync("p1", "missing"),
            () => DeleteAsync("p4", "p4"),
            () => UpdateAsync("p3", 0),
            () => AddBatchAsync(("p2", 10), ("p3", 5)),
            () => DeleteAsync("p2", "p3", "p5"),
            () => AddAsync("p1", 0.1 + 0.2),
            () => UpdateAsync("p1", 0.3),
            () => DeleteAsync("p1")
        ];

        foreach (var mutate in mutations)
        {
            await mutate();

            var result = await Leaderboard.GetEntitiesByRankRangeAsync(Key, 1, expectedScores.Count + 1, RankingType.DenseRank);
            Assert.Equal(expectedScores.Count, result.Length);
            foreach (var player in result)
            {
                Assert.True(expectedScores.TryGetValue(player.Id, out var expectedScore));
                Assert.Equal(expectedScore, player.Score);
                var expectedRank = 1L + expectedScores.Values.Distinct().Count(score => score > expectedScore);
                Assert.Equal(expectedRank, player.Rank);
            }
        }
    }

    private async Task<Player[]> AssertRanksAsync(params (string Id, long Rank)[] expected)
    {
        var result = await Leaderboard.GetEntitiesByRankRangeAsync(Key, 1, expected.Length + 1, RankingType.DenseRank);
        Assert.Equal(expected, result.Select(player => (player.Id, player.Rank)).ToArray());
        return result;
    }
}
