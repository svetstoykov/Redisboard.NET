using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Redisboard.NET.Attributes;
using Redisboard.NET.Common.Models;
using Redisboard.NET.Extensions;
using Redisboard.NET.Helpers;
using Redisboard.NET.Interfaces;
using Redisboard.NET.Serialization;
using StackExchange.Redis;

namespace Redisboard.NET.Tests.Integration;

public class DependencyInjectionTests
{
    [Theory]
    [InlineData(-1, 5)]
    [InlineData(0, 0)]
    [InlineData(6, 6)]
    public async Task AddLeaderboard_WithDatabaseSelection_WritesToSelectedDatabase(int databaseIndex, int expectedDatabase)
    {
        var services = new ServiceCollection();
        Action<ConfigurationOptions> configure = options =>
        {
            options.EndPoints.Add("localhost:6379");
            options.DefaultDatabase = 5;
        };
        if (databaseIndex == -1)
            services.AddLeaderboard<Player>(configure);
        else
            services.AddLeaderboard<Player>(configure, databaseIndex);
        using var provider = services.BuildServiceProvider();
        var leaderboard = provider.GetRequiredService<ILeaderboard<Player>>();
        var connection = provider.GetRequiredService<IConnectionMultiplexer>();
        var key = $"rb004:{Guid.NewGuid():N}";

        try
        {
            await leaderboard.AddEntityAsync(key, new Player { Id = "p1", Score = 42 });

            Assert.Equal(-42, await connection.GetDatabase(expectedDatabase)
                .SortedSetScoreAsync(CacheKey.ForLeaderboardSortedSet(key), "p1"));
            if (expectedDatabase != 0)
                Assert.False(await connection.GetDatabase(0).KeyExistsAsync(CacheKey.ForLeaderboardSortedSet(key)));
        }
        finally
        {
            await leaderboard.DeleteAsync(key);
        }
    }

    [Fact]
    public async Task AddLeaderboard_WithDifferentEntitySerializers_PreservesEachWireFormat()
    {
        using var connection = ConnectionMultiplexer.Connect("localhost:6379");
        var database = connection.GetDatabase(9);
        var services = new ServiceCollection();
        services.AddSingleton<IConnectionMultiplexer>(connection);
        services.AddLeaderboard<JsonEntity>(databaseIndex: 9, serializer: new SystemTextJsonSerializer());
        services.AddLeaderboard<Player>(databaseIndex: 9);
        using var provider = services.BuildServiceProvider();
        var jsonLeaderboard = provider.GetRequiredService<ILeaderboard<JsonEntity>>();
        var memoryPackLeaderboard = provider.GetRequiredService<ILeaderboard<Player>>();
        var jsonKey = $"rb004:json:{Guid.NewGuid():N}";
        var memoryPackKey = $"rb004:memorypack:{Guid.NewGuid():N}";

        try
        {
            await jsonLeaderboard.AddEntityAsync(jsonKey, new JsonEntity { Id = "json", Score = 42 });
            await memoryPackLeaderboard.AddEntityAsync(memoryPackKey, new Player { Id = "memorypack", Score = 24 });

            var jsonBytes = (byte[])(await database.HashGetAsync(CacheKey.ForEntityDataHashSet(jsonKey), "json"))!;
            var memoryPackBytes = (byte[])(await database.HashGetAsync(CacheKey.ForEntityDataHashSet(memoryPackKey), "memorypack"))!;
            Assert.Equal("json", JsonSerializer.Deserialize<JsonEntity>(jsonBytes)!.Id);
            Assert.Equal("memorypack", new MemoryPackLeaderboardSerializer().Deserialize<Player>(memoryPackBytes).Id);
        }
        finally
        {
            await jsonLeaderboard.DeleteAsync(jsonKey);
            await memoryPackLeaderboard.DeleteAsync(memoryPackKey);
        }
    }

    [Fact]
    public async Task Constructor_WithConfiguredDefaultDatabase_WritesToDefaultDatabase()
    {
        using var connection = ConnectionMultiplexer.Connect("localhost:6379,defaultDatabase=5");
        var leaderboard = new Leaderboard<Player>(connection, new MemoryPackLeaderboardSerializer());
        var key = $"rb004:constructor:{Guid.NewGuid():N}";

        try
        {
            await leaderboard.AddEntityAsync(key, new Player { Id = "p1", Score = 42 });

            Assert.Equal(-42, await connection.GetDatabase(5)
                .SortedSetScoreAsync(CacheKey.ForLeaderboardSortedSet(key), "p1"));
        }
        finally
        {
            await leaderboard.DeleteAsync(key);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AddLeaderboard_WithExistingDatabaseSelection_UsesDocumentedRedisService(bool includeMultiplexer)
    {
        using var connection = ConnectionMultiplexer.Connect("localhost:6379");
        var services = new ServiceCollection();
        services.AddSingleton(connection.GetDatabase(8));
        if (includeMultiplexer)
            services.AddSingleton<IConnectionMultiplexer>(connection);
        services.AddLeaderboard<Player>(databaseIndex: 9);
        using var provider = services.BuildServiceProvider();
        var leaderboard = provider.GetRequiredService<ILeaderboard<Player>>();
        var key = $"rb004:reuse:{Guid.NewGuid():N}";

        try
        {
            await leaderboard.AddEntityAsync(key, new Player { Id = "p1", Score = 42 });

            var expectedDatabase = includeMultiplexer ? 9 : 8;
            Assert.Equal(-42, await connection.GetDatabase(expectedDatabase)
                .SortedSetScoreAsync(CacheKey.ForLeaderboardSortedSet(key), "p1"));
        }
        finally
        {
            await leaderboard.DeleteAsync(key);
        }
    }

    [Fact]
    public void AddLeaderboard_WithInternallyCreatedConnection_ProviderDisposesConnection()
    {
        var services = new ServiceCollection();
        services.AddLeaderboard<Player>(options => options.EndPoints.Add("localhost:6379"));
        var provider = services.BuildServiceProvider();
        var connection = provider.GetRequiredService<IConnectionMultiplexer>();

        provider.Dispose();

        Assert.False(connection.IsConnected);
    }

    [Fact]
    public void AddLeaderboard_WithCallerOwnedConnection_ProviderLeavesConnectionUsable()
    {
        using var connection = ConnectionMultiplexer.Connect("localhost:6379");
        var services = new ServiceCollection();
        services.AddSingleton<IConnectionMultiplexer>(connection);
        services.AddLeaderboard<Player>();
        var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<ILeaderboard<Player>>();

        provider.Dispose();

        Assert.True(connection.IsConnected);
    }

    public class JsonEntity : ILeaderboardEntity
    {
        [LeaderboardKey]
        public string Id { get; set; } = "";
        [LeaderboardScore]
        public double Score { get; set; }
        public long Rank { get; set; }
    }

    private sealed class SystemTextJsonSerializer : ILeaderboardSerializer
    {
        public byte[] Serialize<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value);
        public T Deserialize<T>(byte[] data) => JsonSerializer.Deserialize<T>(data)!;
    }
}
