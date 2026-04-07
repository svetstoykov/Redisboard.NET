using FluentAssertions;
using Redisboard.NET.Enumerations;
using StackExchange.Redis;

namespace Redisboard.NET.Tests.Integration;

/// <summary>
/// Tests for <see cref="Leaderboard{T}.DeleteEntityAsync"/> (single entity) and
/// <see cref="Leaderboard{T}.DeleteAsync"/> (entire leaderboard).
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
        neighbours.Should().NotContain(e => e.Id == "del_top");
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

    [Fact]
    public async Task DeleteEntitiesAsync_BatchDelete_RemovesAllEntities()
    {
        await SeedAsync([("batch_del_1", 500.0), ("batch_del_2", 450.0), ("batch_del_3", 400.0)]);

        RedisValue[] keysToDelete = ["batch_del_1", "batch_del_2", "batch_del_3"];

        await Leaderboard.DeleteEntitiesAsync(Key, keysToDelete);

        foreach (var key in keysToDelete)
        {
            var rank = await Leaderboard.GetEntityRankAsync(Key, key);
            var score = await Leaderboard.GetEntityScoreAsync(Key, key);

            rank.Should().BeNull();
            score.Should().BeNull();
        }

        var size = await Leaderboard.GetSizeAsync(Key);
        size.Should().Be(0);
    }

    // --- Dense / Competition ranking correctness after deletions ---

    [Fact]
    public async Task DeleteEntityAsync_UniqueScore_DenseRankCorrectAfterDelete()
    {
        // 4 players with unique scores: dense ranks 1,2,3,4
        await SeedAsync([("p1", 400), ("p2", 300), ("p3", 200), ("p4", 100)]);

        // Delete p2 (score 300, unique) — its score must be removed from unique-score set
        await Leaderboard.DeleteEntityAsync(Key, "p2");

        // Remaining: p1=400 (rank 1), p3=200 (rank 2), p4=100 (rank 3)
        var result = await Leaderboard.GetEntitiesByRankRangeAsync(
            Key, 1, 3, RankingType.DenseRank);

        result.Should().HaveCount(3);
        result.First(p => p.Id == "p1").Rank.Should().Be(1);
        result.First(p => p.Id == "p3").Rank.Should().Be(2);
        result.First(p => p.Id == "p4").Rank.Should().Be(3);
    }

    [Fact]
    public async Task DeleteEntityAsync_SharedScore_DenseRankCorrectAfterDelete()
    {
        // p2 and p3 share score 200 — deleting one should NOT remove the score from unique set
        await SeedAsync([("p1", 400), ("p2", 200), ("p3", 200), ("p4", 100)]);

        await Leaderboard.DeleteEntityAsync(Key, "p2");

        // Remaining: p1=400 (dense 1), p3=200 (dense 2), p4=100 (dense 3)
        var result = await Leaderboard.GetEntitiesByRankRangeAsync(
            Key, 1, 3, RankingType.DenseRank);

        result.Should().HaveCount(3);
        result.First(p => p.Id == "p1").Rank.Should().Be(1);
        result.First(p => p.Id == "p3").Rank.Should().Be(2);
        result.First(p => p.Id == "p4").Rank.Should().Be(3);
    }

    [Fact]
    public async Task DeleteEntityAsync_StandardCompetitionRankCorrectAfterDelete()
    {
        // 5 players: 300, 200, 200, 100, 50
        await SeedAsync([("p1", 300), ("p2", 200), ("p3", 200), ("p4", 100), ("p5", 50)]);

        // Delete p1 (unique score 300)
        await Leaderboard.DeleteEntityAsync(Key, "p1");

        // Remaining with SC: p2=200 (1), p3=200 (1), p4=100 (3), p5=50 (4)
        var result = await Leaderboard.GetEntitiesByRankRangeAsync(
            Key, 1, 4, RankingType.StandardCompetition);

        result.Should().HaveCount(4);
        result.First(p => p.Id == "p2").Rank.Should().Be(1);
        result.First(p => p.Id == "p3").Rank.Should().Be(1);
        result.First(p => p.Id == "p4").Rank.Should().Be(3);
        result.First(p => p.Id == "p5").Rank.Should().Be(4);
    }

    [Fact]
    public async Task DeleteEntityAsync_ModifiedCompetitionRankCorrectAfterDelete()
    {
        // 5 players: 300, 200, 200, 100, 50
        await SeedAsync([("p1", 300), ("p2", 200), ("p3", 200), ("p4", 100), ("p5", 50)]);

        // Delete p1 (unique score 300)
        await Leaderboard.DeleteEntityAsync(Key, "p1");

        // Remaining with MC: p2=200 (2), p3=200 (2), p4=100 (3), p5=50 (4)
        var result = await Leaderboard.GetEntitiesByRankRangeAsync(
            Key, 1, 4, RankingType.ModifiedCompetition);

        result.Should().HaveCount(4);
        result.First(p => p.Id == "p2").Rank.Should().Be(2);
        result.First(p => p.Id == "p3").Rank.Should().Be(2);
        result.First(p => p.Id == "p4").Rank.Should().Be(3);
        result.First(p => p.Id == "p5").Rank.Should().Be(4);
    }

    [Fact]
    public async Task DeleteEntitiesAsync_BatchDelete_DenseRankCorrectAfterDelete()
    {
        // 5 players with unique scores
        await SeedAsync([("p1", 500), ("p2", 400), ("p3", 300), ("p4", 200), ("p5", 100)]);

        // Batch delete p2 and p4 — two unique scores removed
        RedisValue[] keysToDelete = ["p2", "p4"];
        await Leaderboard.DeleteEntitiesAsync(Key, keysToDelete);

        // Remaining: p1=500 (dense 1), p3=300 (dense 2), p5=100 (dense 3)
        var result = await Leaderboard.GetEntitiesByRankRangeAsync(
            Key, 1, 3, RankingType.DenseRank);

        result.Should().HaveCount(3);
        result.First(p => p.Id == "p1").Rank.Should().Be(1);
        result.First(p => p.Id == "p3").Rank.Should().Be(2);
        result.First(p => p.Id == "p5").Rank.Should().Be(3);
    }

    [Fact]
    public async Task DeleteEntityAsync_AllTied_DenseRankCorrectAfterDelete()
    {
        // All players share the same score — only 1 unique score
        await SeedAsync([("p1", 100), ("p2", 100), ("p3", 100)]);

        await Leaderboard.DeleteEntityAsync(Key, "p1");

        // Remaining: p2=100 (dense 1), p3=100 (dense 1) — unique score must still exist
        var result = await Leaderboard.GetEntitiesByRankRangeAsync(
            Key, 1, 2, RankingType.DenseRank);

        result.Should().HaveCount(2);
        result.Should().OnlyContain(p => p.Rank == 1);
    }

    [Fact]
    public async Task DeleteEntityAsync_LastEntityWithScore_DenseRankCorrectAfterDelete()
    {
        // Delete the only entity holding a particular score, then verify no phantom rank gap
        await SeedAsync([("p1", 300), ("p2", 200), ("p3", 100)]);

        // Delete p2 (sole holder of score 200)
        await Leaderboard.DeleteEntityAsync(Key, "p2");

        // Dense ranks should be contiguous: p1=1, p3=2 (NOT p1=1, p3=3 with a phantom gap)
        var rankP1 = await Leaderboard.GetEntityRankAsync(Key, "p1", RankingType.DenseRank);
        var rankP3 = await Leaderboard.GetEntityRankAsync(Key, "p3", RankingType.DenseRank);

        rankP1.Should().Be(1);
        rankP3.Should().Be(2);
    }

    [Fact]
    public async Task DeleteEntitiesAsync_BatchDelete_CompetitionRanksCorrectAfterDelete()
    {
        // 6 players: 500, 400, 300, 300, 200, 100
        await SeedAsync([("p1", 500), ("p2", 400), ("p3", 300), ("p4", 300), ("p5", 200), ("p6", 100)]);

        // Batch delete p2 (unique 400) and p3 (shared 300)
        RedisValue[] keysToDelete = ["p2", "p3"];
        await Leaderboard.DeleteEntitiesAsync(Key, keysToDelete);

        // Remaining SC: p1=500 (1), p4=300 (2), p5=200 (3), p6=100 (4)
        var scResult = await Leaderboard.GetEntitiesByRankRangeAsync(
            Key, 1, 4, RankingType.StandardCompetition);

        scResult.Should().HaveCount(4);
        scResult.First(p => p.Id == "p1").Rank.Should().Be(1);
        scResult.First(p => p.Id == "p4").Rank.Should().Be(2);
        scResult.First(p => p.Id == "p5").Rank.Should().Be(3);
        scResult.First(p => p.Id == "p6").Rank.Should().Be(4);

        // Same data, Dense: p1=1, p4=2, p5=3, p6=4
        var denseResult = await Leaderboard.GetEntitiesByRankRangeAsync(
            Key, 1, 4, RankingType.DenseRank);

        denseResult.Should().HaveCount(4);
        denseResult.First(p => p.Id == "p1").Rank.Should().Be(1);
        denseResult.First(p => p.Id == "p4").Rank.Should().Be(2);
        denseResult.First(p => p.Id == "p5").Rank.Should().Be(3);
        denseResult.First(p => p.Id == "p6").Rank.Should().Be(4);
    }
}
