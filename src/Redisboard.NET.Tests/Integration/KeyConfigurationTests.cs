using Microsoft.Extensions.DependencyInjection;
using Redisboard.NET.Common.Models;
using Redisboard.NET.Enumerations;
using Redisboard.NET.Extensions;
using Redisboard.NET.Helpers;
using Redisboard.NET.Interfaces;
using Redisboard.NET.Serialization;
using StackExchange.Redis;

namespace Redisboard.NET.Tests.Integration;

public class KeyConfigurationTests
{
    [ClusterFact]
    public async Task ClusterKeys_OnRedisCluster_SupportReadsAndMutationsWithoutCrossSlotErrors()
    {
        using var connection = ConnectionMultiplexer.Connect(Environment.GetEnvironmentVariable("REDIS_CLUSTER_CONNECTION")!);
        var database = connection.GetDatabase(0);
        var prefix = $"rb007:{Guid.NewGuid():N}:";
        var key = "board{user-tag}";
        var options = new LeaderboardOptions { KeyPrefix = prefix, UseClusterHashTags = true };
        var leaderboard = new Leaderboard<Player>(database, new MemoryPackLeaderboardSerializer(), options);
        RedisKey[] keys =
        [
            CacheKey.ForLeaderboardSortedSet(key, options),
            CacheKey.ForUniqueScoreSortedSet(key, options),
            CacheKey.ForEntityDataHashSet(key, options)
        ];
        try
        {
            await leaderboard.AddEntityAsync(key, new Player { Id = "one", Score = 10 });
            await leaderboard.AddEntitiesAsync(key, [new Player { Id = "two", Score = 20 }]);
            await leaderboard.UpdateEntityScoreAsync(key, "one", 30);
            await leaderboard.UpdateEntityMetadataAsync(key, new Player { Id = "one", Username = "updated" });

            var incremented = await leaderboard.IncrementEntityScoreAsync(key, "one", 5);
            Assert.Equal(35, incremented.Score);
            var decremented = await leaderboard.DecrementEntityScoreAsync(key, "one", 5);
            Assert.Equal(30, decremented.Score);
            var rejected = await leaderboard.UpdateEntityScoreConditionalAsync(key, "one", 25, ScoreUpdateCondition.Higher);
            Assert.False(rejected.Applied);
            Assert.Equal(30, rejected.Score);
            var inserted = await leaderboard.AddEntityConditionalAsync(key,
                new Player { Id = "atomic", Score = 5 }, ScoreUpdateCondition.NotExists);
            Assert.True(inserted.Applied);
            Assert.Equal(3, inserted.Rank);
            await leaderboard.DeleteEntityAsync(key, "atomic");

            foreach (var redisKey in keys)
                Assert.True(await database.KeyExistsAsync(redisKey));
            Assert.All(keys, redisKey => Assert.InRange(connection.GetHashSlot(redisKey), 0, 16383));
            Assert.Single(keys.Select(connection.GetHashSlot).Distinct());
            Assert.Equal(30, await leaderboard.GetEntityScoreAsync(key, "one"));
            Assert.Equal(2, await leaderboard.GetSizeAsync(key));
            foreach (var ranking in Enum.GetValues<RankingType>())
            {
                Assert.Equal(1, await leaderboard.GetEntityRankAsync(key, "one", ranking));
                var entries = await leaderboard.GetEntitiesByRankRangeAsync(key, 1, 2, ranking);
                Assert.Equal(new[] { "one", "two" }, entries.Select(p => p.Id));
                Assert.Equal("updated", entries[0].Username);
                var scoreEntries = await leaderboard.GetEntitiesByScoreRangeAsync(key, 10, 30, ranking);
                Assert.Equal(2, scoreEntries.Length);
                var neighbours = await leaderboard.GetEntityAndNeighboursAsync(key, "one", rankingType: ranking);
                Assert.NotNull(neighbours);
            }
            await leaderboard.DeleteEntityAsync(key, "one");
            await leaderboard.DeleteEntitiesAsync(key, ["two"]);
            Assert.Equal(0, await leaderboard.GetSizeAsync(key));
            await leaderboard.AddEntityAsync(key, new Player { Id = "three", Score = 3 });
        }
        finally
        {
            await leaderboard.DeleteAsync(key);
        }
        foreach (var redisKey in keys)
            Assert.False(await database.KeyExistsAsync(redisKey));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AtomicWrites_UseConfiguredNamespace(bool cluster)
    {
        using var connection = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
        var database = connection.GetDatabase(9);
        var key = $"atomic{{board}}:{Guid.NewGuid():N}";
        var options = new LeaderboardOptions { KeyPrefix = $"rb008:{Guid.NewGuid():N}:", UseClusterHashTags = cluster };
        var configured = new Leaderboard<Player>(database, new MemoryPackLeaderboardSerializer(), options);
        var legacy = new Leaderboard<Player>(database, new MemoryPackLeaderboardSerializer());
        try
        {
            await configured.AddEntityAsync(key, new Player { Id = "one", Score = 10 });
            await legacy.AddEntityAsync(key, new Player { Id = "one", Score = 1000 });

            var added = await configured.AddEntityConditionalAsync(key,
                new Player { Id = "two", Score = 20 }, ScoreUpdateCondition.NotExists);
            var incremented = await configured.IncrementEntityScoreAsync(key, "one", 20);
            var decremented = await configured.DecrementEntityScoreAsync(key, "one", 5);
            var best = await configured.UpdateEntityScoreConditionalAsync(key, "one", 35, ScoreUpdateCondition.Higher);

            Assert.True(added.Applied);
            Assert.Equal(20, added.Score);
            Assert.Equal(30, incremented.Score);
            Assert.Equal(25, decremented.Score);
            Assert.True(best.Applied);
            Assert.Equal(35, best.Score);
            Assert.Equal(1, best.Rank);
            Assert.Equal(35, await configured.GetEntityScoreAsync(key, "one"));
            Assert.Equal(2, await configured.GetSizeAsync(key));
            Assert.Equal(1000, await legacy.GetEntityScoreAsync(key, "one"));
            Assert.Null(await legacy.GetEntityScoreAsync(key, "two"));
        }
        finally
        {
            await configured.DeleteAsync(key);
            await legacy.DeleteAsync(key);
        }
    }

    private sealed class ClusterFactAttribute : FactAttribute
    {
        public ClusterFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("REDIS_CLUSTER_CONNECTION")))
                Skip = "Set REDIS_CLUSTER_CONNECTION to run against Redis Cluster.";
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfiguredKeys_IsolateBoardsAndSupportEveryOperation(bool cluster)
    {
        using var connection = ConnectionMultiplexer.Connect("localhost:6379");
        var database = connection.GetDatabase(9);
        var prefix = $"rb007:{Guid.NewGuid():N}:";
        var key = "board{untrusted}name";
        var configured = new Leaderboard<Player>(database, new MemoryPackLeaderboardSerializer(),
            new LeaderboardOptions { KeyPrefix = prefix, UseClusterHashTags = cluster });
        var legacy = new Leaderboard<Player>(database, new MemoryPackLeaderboardSerializer());
        try
        {
            await configured.AddEntityAsync(key, new Player { Id = "one", Score = 10 });
            await configured.AddEntitiesAsync(key, [new Player { Id = "two", Score = 20 }]);
            await configured.UpdateEntityScoreAsync(key, "one", 30);
            await configured.UpdateEntityMetadataAsync(key, new Player { Id = "one", Username = "updated" });

            Assert.Equal(0, await legacy.GetSizeAsync(key));
            Assert.Equal(2, await configured.GetSizeAsync(key));
            Assert.Equal(30, await configured.GetEntityScoreAsync(key, "one"));
            foreach (var ranking in Enum.GetValues<RankingType>())
            {
                Assert.Equal(1, await configured.GetEntityRankAsync(key, "one", ranking));
                var entries = await configured.GetEntitiesByRankRangeAsync(key, 1, 2, ranking);
                Assert.Equal(new[] { "one", "two" }, entries.Select(p => p.Id));
                Assert.Equal("updated", entries[0].Username);
            }
            var keys = connection.GetServer(connection.GetEndPoints()[0]).Keys(9, prefix + "*").ToArray();
            Assert.Equal(3, keys.Length);
            if (cluster)
            {
                Assert.Single(keys.Select(HashTag).Distinct());
                var other = new Leaderboard<Player>(database, new MemoryPackLeaderboardSerializer(),
                    new LeaderboardOptions { KeyPrefix = prefix, UseClusterHashTags = true });
                Assert.Equal(2, await other.GetSizeAsync(key));
            }
            await configured.DeleteEntityAsync(key, "one");
            await configured.DeleteEntitiesAsync(key, ["two"]);
            Assert.Equal(0, await configured.GetSizeAsync(key));
            await configured.AddEntityAsync(key, new Player { Id = "three", Score = 3 });
        }
        finally
        {
            await configured.DeleteAsync(key);
        }
        Assert.Empty(connection.GetServer(connection.GetEndPoints()[0]).Keys(9, prefix + "*"));
    }

    private static string HashTag(RedisKey key)
    {
        var text = key.ToString();
        var start = text.IndexOf('{');
        var end = text.IndexOf('}', start + 1);
        Assert.True(start >= 0 && end > start + 1);
        return text[(start + 1)..end];
    }

    [Fact]
    public async Task ClusterKeys_WithSharedUserHashTag_KeepBoardIdentitiesIsolated()
    {
        using var connection = ConnectionMultiplexer.Connect("localhost:6379");
        var prefix = $"rb007:{Guid.NewGuid():N}:";
        var options = new LeaderboardOptions { KeyPrefix = prefix, UseClusterHashTags = true };
        var leaderboard = new Leaderboard<Player>(connection, new MemoryPackLeaderboardSerializer(), options, 9);
        try
        {
            await leaderboard.AddEntityAsync("first{shared}", new Player { Id = "one", Score = 10 });
            await leaderboard.AddEntityAsync("second{shared}", new Player { Id = "two", Score = 20 });

            var keys = connection.GetServer(connection.GetEndPoints()[0]).Keys(9, prefix + "*").ToArray();
            Assert.Equal(6, keys.Length);
            Assert.Equal(2, keys.Select(HashTag).Distinct().Count());
            Assert.Equal(1, await leaderboard.GetSizeAsync("first{shared}"));
            Assert.Equal(1, await leaderboard.GetSizeAsync("second{shared}"));
        }
        finally
        {
            await leaderboard.DeleteAsync("first{shared}");
            await leaderboard.DeleteAsync("second{shared}");
        }
    }

    [Fact]
    public async Task Constructor_WithMutatedOptions_KeepsOriginalNamespace()
    {
        using var connection = ConnectionMultiplexer.Connect("localhost:6379");
        var prefix = $"rb007:{Guid.NewGuid():N}:";
        var options = new LeaderboardOptions { KeyPrefix = prefix };
        var leaderboard = new Leaderboard<Player>(connection.GetDatabase(9), new MemoryPackLeaderboardSerializer(), options);
        options.KeyPrefix = "changed:";
        options.UseClusterHashTags = true;
        try
        {
            await leaderboard.AddEntityAsync("board", new Player { Id = "one", Score = 42 });

            Assert.Equal(-42, await connection.GetDatabase(9).SortedSetScoreAsync(
                prefix + "sorted_set_leaderboard_board", "one"));
        }
        finally
        {
            await leaderboard.DeleteAsync("board");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AddLeaderboard_WithOptions_PreservesRegistrationSettings(bool useMultiplexer)
    {
        using var connection = ConnectionMultiplexer.Connect("localhost:6379");
        var services = new ServiceCollection();
        if (useMultiplexer)
            services.AddSingleton<IConnectionMultiplexer>(connection);
        else
            services.AddSingleton(connection.GetDatabase(9));
        var prefix = $"rb007:{Guid.NewGuid():N}:";
        var options = new LeaderboardOptions { KeyPrefix = prefix };
        services.AddLeaderboard<Player>(optionsAction: null, leaderboardOptions: options, databaseIndex: 9);
        options.KeyPrefix = "changed:";
        using var provider = services.BuildServiceProvider();
        var leaderboard = provider.GetRequiredService<ILeaderboard<Player>>();
        try
        {
            await leaderboard.AddEntityAsync("board", new Player { Id = "one", Score = 42 });

            Assert.Equal(-42, await connection.GetDatabase(9).SortedSetScoreAsync(
                prefix + "sorted_set_leaderboard_board", "one"));
        }
        finally
        {
            await leaderboard.DeleteAsync("board");
        }
    }
}
