#nullable enable
using System.Reflection;
using Redisboard.NET.Attributes;
using Redisboard.NET.Common.Models;
using Redisboard.NET.Helpers;
using Redisboard.NET.Interfaces;
using Redisboard.NET.Enumerations;
using Redisboard.NET.Serialization;
using StackExchange.Redis;

namespace Redisboard.NET.Tests.Integration;

/// <summary>Verifies reads observe one Redis snapshot across concurrent mutations.</summary>
public sealed class ReadSnapshotTests : IAsyncLifetime
{
    private ConnectionMultiplexer _connection = null!;
    private IDatabase _database = null!;
    private Leaderboard<Player> _writer = null!;
    private readonly string _key = $"snapshot_{Guid.NewGuid():N}";

    /// <summary>Connects to Redis and creates an isolated leaderboard.</summary>
    /// <returns>A task that completes after Redis setup.</returns>
    public async Task InitializeAsync()
    {
        _connection = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
        _database = _connection.GetDatabase(9);
        _writer = new Leaderboard<Player>(_database, new MemoryPackLeaderboardSerializer());
    }

    /// <summary>Deletes only this test's keys and releases its Redis connection.</summary>
    /// <returns>A task that completes after cleanup.</returns>
    public async Task DisposeAsync()
    {
        await _writer.DeleteAsync(_key);
        await _connection.DisposeAsync();
    }

    /// <summary>Verifies metadata and score come from the same snapshot.</summary>
    /// <param name="rankingType">Ranking mode to exercise.</param>
    /// <returns>A task that completes after snapshot validation.</returns>
    [Theory]
    [MemberData(nameof(NumericBoundaryTests.RankingModes), MemberType = typeof(NumericBoundaryTests))]
    public async Task RankRange_MetadataChangesAfterFirstRead_ReturnsOriginalMetadata(RankingType rankingType)
    {
        await _writer.AddEntityAsync(_key, new Player { Id = "A", Score = 10, Username = "before" });
        var reader = ReaderAfterFirstRead(() => _writer.UpdateEntityMetadataAsync(
            _key, new Player { Id = "A", Score = 10, Username = "after" }));

        var result = await reader.GetEntitiesByRankRangeAsync(_key, 1, 10, rankingType);

        Assert.Equal("before", Assert.Single(result).Username);
    }

    /// <summary>Verifies a target cannot move out of its own neighborhood between Redis calls.</summary>
    /// <param name="rankingType">Ranking mode to exercise.</param>
    /// <returns>A task that completes after snapshot validation.</returns>
    [Theory]
    [MemberData(nameof(NumericBoundaryTests.RankingModes), MemberType = typeof(NumericBoundaryTests))]
    public async Task Neighbours_TargetMovesAfterFirstRead_ReturnsOriginalTarget(RankingType rankingType)
    {
        await _writer.AddEntitiesAsync(_key, [new Player { Id = "A", Score = 20 }, new Player { Id = "B", Score = 10 }]);
        var reader = ReaderAfterFirstRead(() => _writer.UpdateEntityScoreAsync(_key, "B", 30));

        var result = await reader.GetEntityAndNeighboursAsync(_key, "B", 0, rankingType);

        var entity = Assert.Single(result);
        Assert.Equal("B", entity.Id);
        Assert.Equal(10, entity.Score);
        Assert.Equal(2, entity.Rank);
    }

    /// <summary>Verifies deleting the first matching member cannot invalidate an in-flight score range.</summary>
    /// <param name="rankingType">Ranking mode to exercise.</param>
    /// <returns>A task that completes after snapshot validation.</returns>
    [Theory]
    [MemberData(nameof(NumericBoundaryTests.RankingModes), MemberType = typeof(NumericBoundaryTests))]
    public async Task ScoreRange_FirstMemberDeletedAfterFirstRead_ReturnsOriginalRange(RankingType rankingType)
    {
        await _writer.AddEntitiesAsync(_key, [new Player { Id = "A", Score = 20 }, new Player { Id = "B", Score = 10 }]);
        var reader = ReaderAfterFirstRead(() => _writer.DeleteEntityAsync(_key, "A"));

        var result = await reader.GetEntitiesByScoreRangeAsync(_key, 10, 20, rankingType);

        Assert.Equal(new[] { "A", "B" }, result.Select(p => p.Id));
        Assert.Equal(new[] { 20d, 10d }, result.Select(p => p.Score));
    }

    /// <summary>Verifies rank lookup remains tied to the requested member under concurrent deletion.</summary>
    /// <param name="rankingType">Ranking mode to exercise.</param>
    /// <returns>A task that completes after snapshot validation.</returns>
    [Theory]
    [MemberData(nameof(NumericBoundaryTests.RankingModes), MemberType = typeof(NumericBoundaryTests))]
    public async Task Rank_TargetDeletedAfterFirstRead_ReturnsOriginalRank(RankingType rankingType)
    {
        await _writer.AddEntityAsync(_key, new Player { Id = "A", Score = 10 });
        var reader = ReaderAfterFirstRead(() => _writer.DeleteEntityAsync(_key, "A"));

        var result = await reader.GetEntityRankAsync(_key, "A", rankingType);

        Assert.Equal(1, result);
    }

    /// <summary>Verifies rank queries do not depend on metadata deserialization.</summary>
    /// <param name="rankingType">Ranking mode to exercise.</param>
    /// <returns>A task that completes after checking the rank.</returns>
    [Theory]
    [MemberData(nameof(NumericBoundaryTests.RankingModes), MemberType = typeof(NumericBoundaryTests))]
    public async Task Rank_DeserializerThrows_ReturnsRankWithoutReadingMetadata(RankingType rankingType)
    {
        await _writer.AddEntityAsync(_key, new Player { Id = "A", Score = 10 });
        var reader = new Leaderboard<Player>(_database, new RejectingDeserializer());

        var result = await reader.GetEntityRankAsync(_key, "A", rankingType);

        Assert.Equal(1, result);
    }

    /// <summary>Verifies every returned page remains internally consistent during atomic batch updates.</summary>
    /// <param name="rankingType">Ranking mode to exercise.</param>
    /// <returns>A task that completes after concurrent reads and writes finish.</returns>
    [Theory]
    [MemberData(nameof(NumericBoundaryTests.RankingModes), MemberType = typeof(NumericBoundaryTests))]
    public async Task ConcurrentBatchWrites_ReadsReturnConsistentSnapshots(RankingType rankingType)
    {
        await WriteGenerationAsync(1);

        var writes = Task.Run(async () =>
        {
            for (var generation = 2; generation <= 100; generation++)
                await WriteGenerationAsync(generation);
        });
        try
        {
            for (var i = 0; i < 100; i++)
            {
                var result = (i % 3) switch
                {
                    0 => await _writer.GetEntitiesByRankRangeAsync(_key, 1, 2, rankingType),
                    1 => await _writer.GetEntityAndNeighboursAsync(_key, "A", 1, rankingType),
                    _ => await _writer.GetEntitiesByScoreRangeAsync(_key, 0, 100, rankingType)
                };

                Assert.Equal(new[] { "A", "B" }, result.Select(p => p.Id));
                Assert.Equal(result[0].Username, result[1].Username);
                Assert.All(result, p => Assert.Equal(double.Parse(p.Username!), p.Score));
                var expectedRanks = rankingType switch
                {
                    RankingType.Default => new long[] { 1, 2 },
                    RankingType.ModifiedCompetition => new long[] { 2, 2 },
                    _ => new long[] { 1, 1 }
                };
                Assert.Equal(expectedRanks, result.Select(p => p.Rank));
            }
        }
        finally
        {
            await writes;
        }
    }

    /// <summary>Verifies entity reads preserve arbitrary binary members and serialized metadata.</summary>
    /// <param name="rankingType">Ranking mode to exercise.</param>
    /// <returns>A task that completes after checking exact bytes and missing-metadata fallback.</returns>
    [Theory]
    [MemberData(nameof(NumericBoundaryTests.RankingModes), MemberType = typeof(NumericBoundaryTests))]
    public async Task EntityReads_BinaryMembersAndMetadata_PreserveBytes(RankingType rankingType)
    {
        var reader = new Leaderboard<BinaryEntity>(_database, new BinarySerializer());
        RedisValue member = new byte[] { 0, 255, 254, 128, 65 };
        byte[] payload = [255, 0, 128, 254, 1];
        await reader.AddEntityAsync(_key, new BinaryEntity { Id = member, Score = 0.1 + 0.2, Payload = payload });

        var byRank = Assert.Single(await reader.GetEntitiesByRankRangeAsync(_key, 1, 1, rankingType));
        var byScore = Assert.Single(await reader.GetEntitiesByScoreRangeAsync(_key, 0, 1, rankingType));
        var neighbors = Assert.Single(await reader.GetEntityAndNeighboursAsync(_key, member, 0, rankingType));

        foreach (var entity in new[] { byRank, byScore, neighbors })
        {
            Assert.Equal((byte[])member!, (byte[])entity.Id!);
            Assert.Equal(payload, entity.Payload);
            Assert.Equal(0.1 + 0.2, entity.Score);
            Assert.Equal(1, entity.Rank);
        }
        await _database.HashDeleteAsync(CacheKey.ForEntityDataHashSet(_key), member);
        var fallback = Assert.Single(await reader.GetEntitiesByRankRangeAsync(_key, 1, 1, rankingType));
        Assert.Equal(member, fallback.Id);
        Assert.Equal(0.1 + 0.2, fallback.Score);
        Assert.Empty(fallback.Payload);
    }

    /// <summary>Verifies cancellation requested while Redis returns prevents deserialization.</summary>
    /// <param name="rankingType">Ranking mode to exercise.</param>
    /// <returns>A task that completes after cancellation validation.</returns>
    [Theory]
    [MemberData(nameof(NumericBoundaryTests.RankingModes), MemberType = typeof(NumericBoundaryTests))]
    public async Task EntityRead_CancelledAfterRedisReply_ThrowsBeforeDeserialization(RankingType rankingType)
    {
        await _writer.AddEntityAsync(_key, new Player { Id = "A", Score = 10 });
        using var cancellation = new CancellationTokenSource();
        var reader = ReaderAfterFirstRead(() => { cancellation.Cancel(); return Task.CompletedTask; });

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            reader.GetEntitiesByRankRangeAsync(_key, 1, 1, rankingType, cancellation.Token));
    }

    /// <summary>Verifies rank-only scripts return no rank for an absent member.</summary>
    /// <param name="rankingType">Ranking mode to exercise.</param>
    /// <returns>A task that completes after checking absence.</returns>
    [Theory]
    [MemberData(nameof(NumericBoundaryTests.RankingModes), MemberType = typeof(NumericBoundaryTests))]
    public async Task Rank_MissingMember_ReturnsNull(RankingType rankingType)
    {
        var result = await _writer.GetEntityRankAsync(_key, "missing", rankingType);

        Assert.Null(result);
    }

    /// <summary>Verifies rank-only reads observe cancellation after the Redis response.</summary>
    /// <param name="rankingType">Ranking mode to exercise.</param>
    /// <returns>A task that completes after cancellation validation.</returns>
    [Theory]
    [MemberData(nameof(NumericBoundaryTests.RankingModes), MemberType = typeof(NumericBoundaryTests))]
    public async Task Rank_CancelledAfterRedisReply_Throws(RankingType rankingType)
    {
        await _writer.AddEntityAsync(_key, new Player { Id = "A", Score = 10 });
        using var cancellation = new CancellationTokenSource();
        var reader = ReaderAfterFirstRead(() => { cancellation.Cancel(); return Task.CompletedTask; });

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            reader.GetEntityRankAsync(_key, "A", rankingType, cancellation.Token));
    }

    private Task WriteGenerationAsync(int generation) => _writer.AddEntitiesAsync(_key,
    [
        new Player { Id = "A", Score = generation, Username = generation.ToString() },
        new Player { Id = "B", Score = generation, Username = generation.ToString() }
    ]);

    private sealed class BinarySerializer : ILeaderboardSerializer
    {
        public byte[] Serialize<T>(T value) => ((BinaryEntity)(object)value!).Payload;
        public T Deserialize<T>(byte[] data) => (T)(object)new BinaryEntity { Payload = data };
    }

    private Leaderboard<Player> ReaderAfterFirstRead(Func<Task> mutation)
    {
        var database = DispatchProxy.Create<IDatabase, MutatingDatabaseProxy>();
        var proxy = (MutatingDatabaseProxy)database;
        proxy.Database = _database;
        proxy.AfterRead = mutation;
        return new Leaderboard<Player>(database, new MemoryPackLeaderboardSerializer());
    }

    private sealed class RejectingDeserializer : ILeaderboardSerializer
    {
        public byte[] Serialize<T>(T value) => throw new InvalidOperationException("Serialization is not part of rank lookup.");
        public T Deserialize<T>(byte[] data) => throw new InvalidOperationException("Deserialization is not part of rank lookup.");
    }
}

/// <summary>Injects one real Redis mutation immediately after a read completes.</summary>
/// <remarks>Provides a deterministic command boundary without changing production timing or adding sleeps.</remarks>
public class MutatingDatabaseProxy : DispatchProxy
{
    internal IDatabase Database { get; set; } = null!;
    internal Func<Task> AfterRead { get; set; } = null!;
    private int _readCompleted;

    /// <summary>Forwards commands and inserts the mutation after the first completed read.</summary>
    /// <param name="targetMethod">Redis interface member to forward.</param>
    /// <param name="args">Arguments supplied by the caller.</param>
    /// <returns>The original result or an awaitable that applies the mutation before resuming the reader.</returns>
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var result = targetMethod!.Invoke(Database, args);
        if (targetMethod.ReturnType.IsGenericType && targetMethod.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            return typeof(MutatingDatabaseProxy).GetMethod(nameof(AfterReadAsync), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(targetMethod.ReturnType.GenericTypeArguments[0]).Invoke(this, [result]);
        }
        return result;
    }

    private async Task<T> AfterReadAsync<T>(Task<T> read)
    {
        var result = await read;
        if (Interlocked.Exchange(ref _readCompleted, 1) == 0)
            await AfterRead();
        return result;
    }
}

/// <summary>Represents an entity with binary Redis identity and metadata.</summary>
public sealed class BinaryEntity : ILeaderboardEntity
{
    /// <summary>Gets or sets the exact Redis member bytes.</summary>
    [LeaderboardKey]
    public RedisValue Id { get; set; }

    /// <summary>Gets or sets the leaderboard score.</summary>
    [LeaderboardScore]
    public double Score { get; set; }

    /// <summary>Gets or sets the computed rank.</summary>
    public long Rank { get; set; }

    /// <summary>Gets or sets the serialized metadata bytes.</summary>
    public byte[] Payload { get; set; } = [];
}
