using FluentAssertions;
using Redisboard.NET.Common.Models;
using Redisboard.NET.Exceptions;

namespace Redisboard.NET.Tests.Integration;

/// <summary>
/// Tests for optimistic concurrency versioned score updates via
/// <see cref="Leaderboard{T}.UpdateEntityScoreWithVersionAsync"/>.
/// </summary>
public class ConcurrencyTests : LeaderboardTestBase
{
    public ConcurrencyTests(LeaderboardFixture fixture) : base(fixture) { }

    [Fact]
    public async Task UpdateEntityScoreWithVersionAsync_InitialVersion_SucceedsAndIncrements()
    {
        await SeedAsync([("conv_p1", 100.0)]);

        var neighbours = await Leaderboard.GetEntityAndNeighboursAsync(Key, "conv_p1", offset: 0);
        var player = neighbours.First();
        var originalVersion = player.Version;

        player.Score = 200;

        await Leaderboard.UpdateEntityScoreWithVersionAsync(Key, player);

        var score = await Leaderboard.GetEntityScoreAsync(Key, "conv_p1");
        score.Should().Be(200);

        var updated = await Leaderboard.GetEntityAndNeighboursAsync(Key, "conv_p1", offset: 0);
        updated.First().Version.Should().BeGreaterThan(originalVersion);
    }

    [Fact]
    public async Task UpdateEntityScoreWithVersionAsync_ConcurrentUpdate_ThrowsConcurrencyException()
    {
        await SeedAsync([("conv_race", 50.0)]);

        var read1 = await Leaderboard.GetEntityAndNeighboursAsync(Key, "conv_race", offset: 0);
        var read2 = await Leaderboard.GetEntityAndNeighboursAsync(Key, "conv_race", offset: 0);

        var player1 = read1.First();
        var player2 = read2.First();

        player1.Score = 100;
        await Leaderboard.UpdateEntityScoreWithVersionAsync(Key, player1);

        player2.Score = 200;
        var ex = await Assert.ThrowsAsync<ConcurrencyException>(
            async () => await Leaderboard.UpdateEntityScoreWithVersionAsync(Key, player2));

        ex.EntityKey.Should().Be((StackExchange.Redis.RedisValue)"conv_race");
    }

    [Fact]
    public async Task UpdateEntityScoreWithVersionAsync_RetryAfterConflict_Succeeds()
    {
        await SeedAsync([("conv_retry", 10.0)]);

        var read = await Leaderboard.GetEntityAndNeighboursAsync(Key, "conv_retry", offset: 0);
        var player = read.First();
        player.Score = 20;
        await Leaderboard.UpdateEntityScoreWithVersionAsync(Key, player);

        var refreshed = await Leaderboard.GetEntityAndNeighboursAsync(Key, "conv_retry", offset: 0);
        var fresh = refreshed.First();
        fresh.Score = 30;
        await Leaderboard.UpdateEntityScoreWithVersionAsync(Key, fresh);

        var finalScore = await Leaderboard.GetEntityScoreAsync(Key, "conv_retry");
        finalScore.Should().Be(30);
    }

    [Fact]
    public async Task UpdateEntityScoreWithVersionAsync_ExplicitKeyAndScore_Succeeds()
    {
        await SeedAsync([("conv_explicit", 42.0)]);

        var newVersion = await Leaderboard.UpdateEntityScoreWithVersionAsync(Key, "conv_explicit", 999.0, expectedVersion: 1);

        newVersion.Should().Be(2);

        var score = await Leaderboard.GetEntityScoreAsync(Key, "conv_explicit");
        score.Should().Be(999);
    }

    [Fact]
    public async Task UpdateEntityScoreWithVersionAsync_ExplicitKeyAndScore_WrongVersion_Throws()
    {
        await SeedAsync([("conv_wrong", 10.0)]);

        await Assert.ThrowsAsync<ConcurrencyException>(
            async () => await Leaderboard.UpdateEntityScoreWithVersionAsync(Key, "conv_wrong", 500.0, expectedVersion: 99));
    }

    [Fact]
    public async Task UpdateEntityScoreWithVersionAsync_NonExistentEntity_ThrowsConcurrencyException()
    {
        await Assert.ThrowsAsync<ConcurrencyException>(
            async () => await Leaderboard.UpdateEntityScoreWithVersionAsync(Key, "ghost_version", 100.0, expectedVersion: 0));
    }
}
