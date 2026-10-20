using Redisboard.NET.Common.Models;
using Redisboard.NET.Enumerations;
using Redisboard.NET.Helpers;
using StackExchange.Redis;

namespace Redisboard.NET.Tests.Integration;

public class ReadAndExpiryTests(LeaderboardFixture fixture) : LeaderboardTestBase(fixture)
{
    [Theory]
    [InlineData(RankingType.Default, 3)]
    [InlineData(RankingType.DenseRank, 2)]
    [InlineData(RankingType.StandardCompetition, 2)]
    [InlineData(RankingType.ModifiedCompetition, 3)]
    public async Task GetEntityAsync_ReturnsPayloadAndCurrentScoreAndRank(RankingType ranking, long expected)
    {
        await Leaderboard.AddEntitiesAsync(Key, [new Player { Id = "a", Score = 100 }, new Player { Id = "b", Score = 80 }, new Player { Id = "c", Score = 80, Username = "original" }]);
        await Leaderboard.UpdateEntityScoreAsync(Key, "c", 80);

        var entity = await Leaderboard.GetEntityAsync(Key, "c", ranking);

        Assert.NotNull(entity);
        Assert.Equal("original", entity.Username);
        Assert.Equal(80, entity.Score);
        Assert.Equal(expected, entity.Rank);
        Assert.Null(await Leaderboard.GetEntityAsync(Key, "missing", ranking));
    }

    [Fact]
    public async Task GetEntitiesByKeysAsync_PreservesInputOrderAndDuplicatesAndOmitsMissing()
    {
        await SeedAsync([("a", 100), ("b", 80), ("c", 80)]);

        var result = await Leaderboard.GetEntitiesByKeysAsync(Key, ["c", "missing", "a", "c"], RankingType.DenseRank);

        Assert.Equal(new[] { "c", "a", "c" }, result.Select(p => p.Id));
        Assert.Equal(new long[] { 2, 1, 2 }, result.Select(p => p.Rank));
    }

    [Theory]
    [InlineData(RankingType.Default)]
    [InlineData(RankingType.DenseRank)]
    [InlineData(RankingType.StandardCompetition)]
    [InlineData(RankingType.ModifiedCompetition)]
    public async Task GetPageAsync_UsesPhysicalPositionsAndReturnsAuthoritativeTotal(RankingType ranking)
    {
        await SeedAsync([("a", 100), ("b", 80), ("c", 80), ("d", 40)]);

        var page = await Leaderboard.GetPageAsync(Key, 2, 2, ranking);

        Assert.Equal(4, page.TotalCount);
        Assert.Equal(new[] { "c", "d" }, page.Entities.Select(p => p.Id));
        var empty = await Leaderboard.GetPageAsync(Key, 10, 2, ranking);
        Assert.Empty(empty.Entities);
        Assert.Equal(4, empty.TotalCount);
    }

    [Fact]
    public async Task GetPageAsync_EmptyBoardReturnsZeroTotal()
    {
        var page = await Leaderboard.GetPageAsync(Key, 1, 10);

        Assert.Empty(page.Entities);
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task ExpireAsync_SetsSameDeadlineOnAllStructuresAndCanRemoveExpiry()
    {
        await SeedAsync([("a", 100)]);
        using var connection = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
        var db = connection.GetDatabase(9);
        RedisKey[] keys = [CacheKey.ForLeaderboardSortedSet(Key), CacheKey.ForUniqueScoreSortedSet(Key), CacheKey.ForEntityDataHashSet(Key)];

        Assert.True(await Leaderboard.ExpireAsync(Key, TimeSpan.FromMinutes(5)));

        var deadlines = await Task.WhenAll(keys.Select(k => db.KeyExpireTimeAsync(k)));
        Assert.All(deadlines, value => Assert.NotNull(value));
        Assert.All(deadlines, value => Assert.Equal(deadlines[0], value));
        Assert.True(await Leaderboard.ExpireAsync(Key, null));
        foreach (var key in keys) Assert.Null(await db.KeyTimeToLiveAsync(key));
    }

    [Fact]
    public async Task ExpireAsync_ZeroDeletesAllStructures()
    {
        await SeedAsync([("a", 100)]);

        Assert.True(await Leaderboard.ExpireAsync(Key, TimeSpan.Zero));

        Assert.Equal(0, await Leaderboard.GetSizeAsync(Key));
        Assert.Null(await Leaderboard.GetEntityAsync(Key, "a"));
        Assert.False(await Leaderboard.ExpireAsync(Key, TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public async Task GetPageAsync_ConcurrentBatchReplacementKeepsRowsAndTotalTogether()
    {
        await SeedAsync([("a", 100)]);
        var writer = Task.Run(async () =>
        {
            for (var i = 0; i < 60; i++)
            {
                await Leaderboard.DeleteAsync(Key);
                await Leaderboard.AddEntitiesAsync(Key, [new Player { Id = "a", Score = 100 }, new Player { Id = "b", Score = 80 }]);
            }
        });

        for (var i = 0; i < 100; i++)
        {
            var page = await Leaderboard.GetPageAsync(Key, 1, 10);
            Assert.Equal(page.TotalCount, page.Entities.LongLength);
        }
        await writer;
    }

    [Fact]
    public async Task NewReads_LegacyMissingMetadataUsesRankingAndTotal()
    {
        await SeedAsync([("a", 100)]);
        using var connection = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
        await connection.GetDatabase(9).HashDeleteAsync(CacheKey.ForEntityDataHashSet(Key), "a");

        var entity = await Leaderboard.GetEntityAsync(Key, "a");
        var page = await Leaderboard.GetPageAsync(Key, 1, 10);

        Assert.NotNull(entity);
        Assert.Equal("a", entity.Id);
        Assert.Equal(100, entity.Score);
        Assert.Equal(1, entity.Rank);
        Assert.Equal(1, page.TotalCount);
        Assert.Single(page.Entities);
    }

    [Fact]
    public async Task GetPageAsync_MaximumPageNumberDoesNotOverflow()
    {
        await SeedAsync([("a", 100)]);

        var page = await Leaderboard.GetPageAsync(Key, int.MaxValue, 10_000);

        Assert.Empty(page.Entities);
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task NewOperations_RejectInvalidInputsBeforeRedis()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Leaderboard.GetPageAsync(Key, 0, 1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Leaderboard.GetPageAsync(Key, 1, 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Leaderboard.GetPageAsync(Key, 1, 10001));
        await Assert.ThrowsAsync<ArgumentException>(() => Leaderboard.GetEntitiesByKeysAsync(Key, []));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Leaderboard.ExpireAsync(Key, TimeSpan.FromMilliseconds(-1)));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Leaderboard.GetEntityAsync(Key, "a", cancellationToken: canceled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Leaderboard.GetPageAsync(Key, 1, 10, cancellationToken: canceled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Leaderboard.GetEntitiesByKeysAsync(Key, ["a"], cancellationToken: canceled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Leaderboard.ExpireAsync(Key, null, canceled.Token));
    }
}
