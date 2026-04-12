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
        // Arrange
        await SeedAsync([("del_top", 100.0), ("del_mid", 90.0), ("del_bot", 80.0)]);

        // Act
        await Leaderboard.DeleteEntityAsync(Key, "del_top");

        // Assert
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
        // Arrange
        await SeedAsync([("keep_a", 300.0), ("keep_b", 200.0), ("keep_c", 100.0), ("remove", 150.0)]);

        // Act
        await Leaderboard.DeleteEntityAsync(Key, "remove");

        // Assert
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
        // Arrange
        await SeedAsync(Enumerable.Range(0, 50).Select(i => ($"d_{i}", (double)i)));

        // Act
        await Leaderboard.DeleteAsync(Key);

        // Assert
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
        // Arrange
        await SeedAsync([("batch_del_1", 500.0), ("batch_del_2", 450.0), ("batch_del_3", 400.0)]);

        RedisValue[] keysToDelete = ["batch_del_1", "batch_del_2", "batch_del_3"];

        // Act
        await Leaderboard.DeleteEntitiesAsync(Key, keysToDelete);

        // Assert
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

    [Fact]
    public async Task DeleteEntityAsync_UniqueScore_DenseRankCorrectAfterDelete()
    {
        // Arrange
        await SeedAsync([("p1", 400), ("p2", 300), ("p3", 200), ("p4", 100)]);

        // Act
        await Leaderboard.DeleteEntityAsync(Key, "p2");

        // Assert
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
        // Arrange
        await SeedAsync([("p1", 400), ("p2", 200), ("p3", 200), ("p4", 100)]);

        // Act
        await Leaderboard.DeleteEntityAsync(Key, "p2");

        // Assert
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
        // Arrange
        await SeedAsync([("p1", 300), ("p2", 200), ("p3", 200), ("p4", 100), ("p5", 50)]);

        // Act
        await Leaderboard.DeleteEntityAsync(Key, "p1");

        // Assert
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
        // Arrange
        await SeedAsync([("p1", 300), ("p2", 200), ("p3", 200), ("p4", 100), ("p5", 50)]);

        // Act
        await Leaderboard.DeleteEntityAsync(Key, "p1");

        // Assert
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
        // Arrange
        await SeedAsync([("p1", 500), ("p2", 400), ("p3", 300), ("p4", 200), ("p5", 100)]);

        RedisValue[] keysToDelete = ["p2", "p4"];

        // Act
        await Leaderboard.DeleteEntitiesAsync(Key, keysToDelete);

        // Assert
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
        // Arrange
        await SeedAsync([("p1", 100), ("p2", 100), ("p3", 100)]);

        // Act
        await Leaderboard.DeleteEntityAsync(Key, "p1");

        // Assert
        var result = await Leaderboard.GetEntitiesByRankRangeAsync(
            Key, 1, 2, RankingType.DenseRank);

        result.Should().HaveCount(2);
        result.Should().OnlyContain(p => p.Rank == 1);
    }

    [Fact]
    public async Task DeleteEntityAsync_LastEntityWithScore_DenseRankCorrectAfterDelete()
    {
        // Arrange
        await SeedAsync([("p1", 300), ("p2", 200), ("p3", 100)]);

        // Act
        await Leaderboard.DeleteEntityAsync(Key, "p2");

        // Assert
        var rankP1 = await Leaderboard.GetEntityRankAsync(Key, "p1", RankingType.DenseRank);
        var rankP3 = await Leaderboard.GetEntityRankAsync(Key, "p3", RankingType.DenseRank);

        rankP1.Should().Be(1);
        rankP3.Should().Be(2);
    }

    [Fact]
    public async Task DeleteEntitiesAsync_BatchDelete_StandardCompetitionRanksRemainCorrect()
    {
        // Arrange
        await SeedAsync([("p1", 500), ("p2", 400), ("p3", 300), ("p4", 300), ("p5", 200), ("p6", 100)]);

        RedisValue[] keysToDelete = ["p2", "p3"];

        // Act
        await Leaderboard.DeleteEntitiesAsync(Key, keysToDelete);

        // Assert
        var scResult = await Leaderboard.GetEntitiesByRankRangeAsync(
            Key, 1, 4, RankingType.StandardCompetition);

        scResult.Should().HaveCount(4);
        scResult.First(p => p.Id == "p1").Rank.Should().Be(1);
        scResult.First(p => p.Id == "p4").Rank.Should().Be(2);
        scResult.First(p => p.Id == "p5").Rank.Should().Be(3);
        scResult.First(p => p.Id == "p6").Rank.Should().Be(4);

    }

    [Fact]
    public async Task DeleteEntitiesAsync_BatchDelete_DenseRanksRemainCorrect()
    {
        // Arrange
        await SeedAsync([("p1", 500), ("p2", 400), ("p3", 300), ("p4", 300), ("p5", 200), ("p6", 100)]);

        RedisValue[] keysToDelete = ["p2", "p3"];

        // Act
        await Leaderboard.DeleteEntitiesAsync(Key, keysToDelete);

        // Assert
        var denseResult = await Leaderboard.GetEntitiesByRankRangeAsync(
            Key, 1, 4, RankingType.DenseRank);

        denseResult.Should().HaveCount(4);
        denseResult.First(p => p.Id == "p1").Rank.Should().Be(1);
        denseResult.First(p => p.Id == "p4").Rank.Should().Be(2);
        denseResult.First(p => p.Id == "p5").Rank.Should().Be(3);
        denseResult.First(p => p.Id == "p6").Rank.Should().Be(4);
    }
}
