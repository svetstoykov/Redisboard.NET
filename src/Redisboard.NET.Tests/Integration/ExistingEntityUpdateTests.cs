using Redisboard.NET.Common.Models;
using Redisboard.NET.Enumerations;
using Redisboard.NET.Helpers;
using StackExchange.Redis;

namespace Redisboard.NET.Tests.Integration;

/// <summary>Verifies updates preserve complete leaderboard records.</summary>
public class ExistingEntityUpdateTests : LeaderboardTestBase
{
    /// <summary>Initializes tests with isolated leaderboard keys.</summary>
    /// <param name="fixture">Provides the Redis-backed leaderboard.</param>
    public ExistingEntityUpdateTests(LeaderboardFixture fixture) : base(fixture) { }

    /// <summary>Verifies score updates do not create missing entities.</summary>
    /// <param name="useEntity">Whether to use the entity overload.</param>
    /// <param name="fireAndForget">Whether to omit the write acknowledgement.</param>
    /// <returns>A task that completes after verifying persisted state.</returns>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task UpdateEntityScoreAsync_MissingEntity_DoesNotCreateRecord(bool useEntity, bool fireAndForget)
    {
        await SeedAsync([("existing", 10)]);

        if (useEntity)
            await Leaderboard.UpdateEntityScoreAsync(Key, new Player { Id = "missing", Score = 20 }, fireAndForget);
        else
            await Leaderboard.UpdateEntityScoreAsync(Key, "missing", 20, fireAndForget);

        Assert.Null(await Leaderboard.GetEntityScoreAsync(Key, "missing"));
        var player = Assert.Single(await Leaderboard.GetEntitiesByRankRangeAsync(Key, 1, 10, RankingType.DenseRank));
        Assert.Equal("existing", player.Id);
        Assert.Equal(1, player.Rank);
        Assert.Equal(1, await Leaderboard.GetSizeAsync(Key));
    }

    /// <summary>Verifies metadata updates do not create missing entities.</summary>
    /// <param name="fireAndForget">Whether to omit the write acknowledgement.</param>
    /// <returns>A task that completes after checking the metadata hash.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UpdateEntityMetadataAsync_MissingEntity_DoesNotCreateRecord(bool fireAndForget)
    {
        await SeedAsync([("existing", 10)]);
        using var connection = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
        var database = connection.GetDatabase(9);

        await Leaderboard.UpdateEntityMetadataAsync(Key, new Player { Id = "missing", Username = "ghost" }, fireAndForget);

        Assert.Equal(1, await Leaderboard.GetSizeAsync(Key));
        Assert.False(await database.HashExistsAsync(CacheKey.ForEntityDataHashSet(Key), "missing"));
    }

    /// <summary>Verifies size follows ranked membership when legacy metadata is inconsistent.</summary>
    /// <returns>A task that completes after checking the authoritative member count.</returns>
    [Fact]
    public async Task GetSizeAsync_InconsistentLegacyMetadata_CountsRankedMembers()
    {
        await SeedAsync([("ranked", 10)]);
        using var connection = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
        var database = connection.GetDatabase(9);
        await database.HashDeleteAsync(CacheKey.ForEntityDataHashSet(Key), "ranked");
        await database.HashSetAsync(CacheKey.ForEntityDataHashSet(Key), "orphan1", "metadata");
        await database.HashSetAsync(CacheKey.ForEntityDataHashSet(Key), "orphan2", "metadata");

        var size = await Leaderboard.GetSizeAsync(Key);

        Assert.Equal(1, size);
        Assert.Single(await Leaderboard.GetEntitiesByRankRangeAsync(Key, 1, 10));
    }

    /// <summary>Verifies a score update cannot mutate a legacy member without metadata.</summary>
    /// <returns>A task that completes after checking the unchanged score.</returns>
    [Fact]
    public async Task UpdateEntityScoreAsync_MetadataMissing_DoesNotMutatePartialRecord()
    {
        await SeedAsync([("partial", 10)]);
        using var connection = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
        await connection.GetDatabase(9).HashDeleteAsync(CacheKey.ForEntityDataHashSet(Key), "partial");

        await Leaderboard.UpdateEntityScoreAsync(Key, "partial", 20);

        Assert.Equal(10, await Leaderboard.GetEntityScoreAsync(Key, "partial"));
    }

    /// <summary>Verifies metadata updates preserve the stored score and dense ranks.</summary>
    /// <returns>A task that completes after reading independently persisted metadata.</returns>
    [Fact]
    public async Task UpdateEntityMetadataAsync_ExistingEntity_PreservesScoreAndRank()
    {
        await SeedAsync([("existing", 10), ("other", 5)]);

        await Leaderboard.UpdateEntityMetadataAsync(Key, new Player { Id = "existing", Score = 999, Username = "updated" });

        var player = Assert.Single(await Leaderboard.GetEntitiesByRankRangeAsync(Key, 1, 1, RankingType.DenseRank));
        Assert.Equal("updated", player.Username);
        Assert.Equal(10, player.Score);
        Assert.Equal(1, player.Rank);
    }

    /// <summary>Verifies a metadata update cannot repair a partial legacy record implicitly.</summary>
    /// <returns>A task that completes after verifying the hash field remains absent.</returns>
    [Fact]
    public async Task UpdateEntityMetadataAsync_MetadataMissing_DoesNotCreateMetadata()
    {
        await SeedAsync([("partial", 10)]);
        using var connection = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
        var database = connection.GetDatabase(9);
        await database.HashDeleteAsync(CacheKey.ForEntityDataHashSet(Key), "partial");

        await Leaderboard.UpdateEntityMetadataAsync(Key, new Player { Id = "partial", Username = "replacement" });

        Assert.False(await database.HashExistsAsync(CacheKey.ForEntityDataHashSet(Key), "partial"));
        Assert.Equal(10, await Leaderboard.GetEntityScoreAsync(Key, "partial"));
    }

    /// <summary>Verifies deletion racing either update cannot leave a ghost record.</summary>
    /// <param name="updateMetadata">Whether the competing update replaces metadata.</param>
    /// <returns>A task that completes after checking both Redis structures.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UpdateAsync_ConcurrentDeletion_DoesNotLeavePartialRecord(bool updateMetadata)
    {
        using var connection = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
        var database = connection.GetDatabase(9);
        for (var i = 0; i < 20; i++)
        {
            await Leaderboard.AddEntityAsync(Key, new Player { Id = "racing", Score = 10 });

            await Task.WhenAll(
                Leaderboard.DeleteEntityAsync(Key, "racing"),
                updateMetadata
                    ? Leaderboard.UpdateEntityMetadataAsync(Key, new Player { Id = "racing", Username = "updated" })
                    : Leaderboard.UpdateEntityScoreAsync(Key, "racing", 20));

            Assert.Null(await Leaderboard.GetEntityScoreAsync(Key, "racing"));
            Assert.False(await database.HashExistsAsync(CacheKey.ForEntityDataHashSet(Key), "racing"));
            Assert.Equal(0, await Leaderboard.GetSizeAsync(Key));
        }
    }

    /// <summary>Verifies cancellation prevents a metadata write from being submitted.</summary>
    /// <returns>A task that completes after checking the persisted metadata remains unchanged.</returns>
    [Fact]
    public async Task UpdateEntityMetadataAsync_Canceled_DoesNotChangeMetadata()
    {
        await Leaderboard.AddEntityAsync(Key, new Player { Id = "existing", Score = 10, Username = "original" });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Leaderboard.UpdateEntityMetadataAsync(
            Key, new Player { Id = "existing", Username = "updated" }, cancellationToken: new CancellationToken(true)));

        var player = Assert.Single(await Leaderboard.GetEntitiesByRankRangeAsync(Key, 1, 1));
        Assert.Equal("original", player.Username);
    }
}
