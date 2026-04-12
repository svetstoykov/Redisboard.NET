using System.Text.Json;
using Moq;
using Redisboard.NET.Common.Models;
using Redisboard.NET.Helpers;
using Redisboard.NET.Serialization;
using StackExchange.Redis;

namespace Redisboard.NET.Tests.UnitTests;

public class LeaderboardTests
{
    private const string LeaderboardKey = nameof(LeaderboardKey);

    private readonly string _sortedSetKey = CacheKey.ForLeaderboardSortedSet(LeaderboardKey);
    private readonly string _hashSetKey = CacheKey.ForEntityDataHashSet(LeaderboardKey);
    private readonly string _uniqueSortedSetKey = CacheKey.ForUniqueScoreSortedSet(LeaderboardKey);

    private readonly Mock<IDatabase> _mockRedis = new();
    private readonly Random _random = new();

    [Fact]
    public async Task AddEntityAsync_WithInvalidLeaderboardKey_ThrowsArgumentNullException()
    {
        var entity = Player.New();

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object, new MemoryPackLeaderboardSerializer());

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.AddEntityAsync(default, entity));
    }

    [Fact]
    public async Task AddEntityAsync_WithValidEntity_AddsEntityToLeaderboard()
    {
        var entity = Player.New();
        var invertedScore = -entity.Score;

        _mockRedis.Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                CommandFlags.None))
            .ReturnsAsync(RedisResult.Create(1))
            .Verifiable();

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object, new MemoryPackLeaderboardSerializer());

        await leaderboard.AddEntityAsync(LeaderboardKey, entity);

        _mockRedis.Verify();

        _mockRedis.Verify(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.Is<RedisKey[]>(keys =>
                    keys.Length == 3 &&
                    keys[0] == _sortedSetKey &&
                    keys[1] == _uniqueSortedSetKey &&
                    keys[2] == _hashSetKey),
                It.Is<RedisValue[]>(args =>
                    args.Length == 3 &&
                    args[0] == entity.Id &&
                    args[1] == invertedScore &&
                    args[2].HasValue),
                CommandFlags.None),
            Times.Once);
    }

    [Fact]
    public async Task AddEntitiesAsync_WithNullEntities_ThrowsArgumentNullException()
    {
        var leaderboard = new Leaderboard<Player>(_mockRedis.Object, new MemoryPackLeaderboardSerializer());

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.AddEntitiesAsync(LeaderboardKey, null!));
    }

    [Fact]
    public async Task AddEntitiesAsync_WithEmptyEntities_ThrowsArgumentException()
    {
        var leaderboard = new Leaderboard<Player>(_mockRedis.Object, new MemoryPackLeaderboardSerializer());

        await Assert.ThrowsAsync<ArgumentException>(
            async () => await leaderboard.AddEntitiesAsync(LeaderboardKey, []));
    }

    [Fact]
    public async Task AddEntitiesAsync_WithTooManyEntities_ThrowsArgumentOutOfRangeException()
    {
        var entries = Enumerable.Range(0, Leaderboard<Player>.DefaultMaxBatchOperationSize + 1)
            .Select(i => new Player { Id = $"p_{i}", Score = i });

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object, new MemoryPackLeaderboardSerializer());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await leaderboard.AddEntitiesAsync(LeaderboardKey, entries));
    }

    [Fact]
    public async Task AddEntitiesAsync_WithValidEntities_ExecutesBatchScript()
    {
        var entries = new[]
        {
            new Player { Id = "batch_1", Score = 100 },
            new Player { Id = "batch_2", Score = 99 }
        };

        _mockRedis.Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                CommandFlags.None))
            .ReturnsAsync(RedisResult.Create(1))
            .Verifiable();

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object, new MemoryPackLeaderboardSerializer());

        await leaderboard.AddEntitiesAsync(LeaderboardKey, entries);

        _mockRedis.Verify();
    }

    [Fact]
    public async Task UpdateEntityScoreAsync_WithInvalidLeaderboardKey_ThrowsArgumentNullException()
    {
        var entity = Player.New();

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object, new MemoryPackLeaderboardSerializer());

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.UpdateEntityScoreAsync(default, entity));
    }

    [Fact]
    public async Task UpdateEntityScoreAsync_WithValidEntity_UpdatesScore()
    {
        var entity = Player.New();

        _mockRedis.Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(Array.Empty<RedisValue>()))
            .Verifiable();

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object, new MemoryPackLeaderboardSerializer());

        await leaderboard.UpdateEntityScoreAsync(LeaderboardKey, entity);

        _mockRedis.Verify();
    }

    [Fact]
    public async Task UpdateEntityMetadataAsync_WithInvalidLeaderboardKey_ThrowsArgumentNullException()
    {
        var entity = Player.New();

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object, new MemoryPackLeaderboardSerializer());

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.UpdateEntityMetadataAsync(default, entity));
    }

    [Fact]
    public async Task UpdateEntityMetadataAsync_WithValidEntity_UpdatesMetadata()
    {
        var entity = Player.New();

        _mockRedis.Setup(db => db.HashSetAsync(
                _hashSetKey,
                entity.Id,
                It.IsAny<RedisValue>(),
                When.Always,
                CommandFlags.None))
            .ReturnsAsync(true)
            .Verifiable();

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object, new MemoryPackLeaderboardSerializer());

        await leaderboard.UpdateEntityMetadataAsync(LeaderboardKey, entity);

        _mockRedis.Verify();
    }

    [Fact]
    public async Task GetEntityAndNeighboursAsync_WithInvalidLeaderboardKey_ThrowsArgumentNullException()
    {
        var entity = Player.New();

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object, new MemoryPackLeaderboardSerializer());

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.GetEntityAndNeighboursAsync(default, entity.Id));
    }

    [Fact]
    public async Task GetEntityAndNeighboursAsync_WithInvalidEntityKey_ThrowsArgumentNullException()
    {
        var leaderboard = new Leaderboard<Player>(_mockRedis.Object, new MemoryPackLeaderboardSerializer());

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.GetEntityAndNeighboursAsync(LeaderboardKey, default(string)));
    }

    [Fact]
    public async Task GetEntityAndNeighboursAsync_WithInvalidOffset_ThrowsArgumentOutOfRangeException()
    {
        var entity = Player.New();
        const int invalidOffset = -100;

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object, new MemoryPackLeaderboardSerializer());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await leaderboard.GetEntityAndNeighboursAsync(LeaderboardKey, entity.Id, invalidOffset));
    }

    [Fact]
    public async Task GetEntitiesByScoreRangeAsync_WithInvalidLeaderboardKey_ThrowsArgumentNullException()
    {
        var leaderboard = new Leaderboard<Player>(_mockRedis.Object, new MemoryPackLeaderboardSerializer());

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.GetEntitiesByScoreRangeAsync(default, 0, 100));
    }

    [Fact]
    public async Task GetEntitiesByScoreRangeAsync_WithInvalidMinScore_ThrowsArgumentException()
    {
        var leaderboard = new Leaderboard<Player>(_mockRedis.Object, new MemoryPackLeaderboardSerializer());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await leaderboard.GetEntitiesByScoreRangeAsync(LeaderboardKey, -100, 100));
    }

    [Fact]
    public async Task GetEntitiesByScoreRangeAsync_WithInvalidMaxScore_ThrowsArgumentException()
    {
        var leaderboard = new Leaderboard<Player>(_mockRedis.Object, new MemoryPackLeaderboardSerializer());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await leaderboard.GetEntitiesByScoreRangeAsync(LeaderboardKey, 0, -100));
    }

    [Fact]
    public async Task GetEntitiesByScoreRangeAsync_WithInvalidScoreRange_ThrowsInvalidOperationException()
    {
        var leaderboard = new Leaderboard<Player>(_mockRedis.Object, new MemoryPackLeaderboardSerializer());

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await leaderboard.GetEntitiesByScoreRangeAsync(LeaderboardKey, 90, 20));
    }

    [Fact]
    public async Task GetEntityScoreAsync_WithValidEntityKey_ReturnsScore()
    {
        var entity = Player.New();
        var expectedScore = entity.Score;

        _mockRedis.Setup(x => x.SortedSetScoreAsync(
                _sortedSetKey,
                entity.Id,
                CommandFlags.None))
            .ReturnsAsync(-expectedScore)
            .Verifiable();

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object, new MemoryPackLeaderboardSerializer());

        var result = await leaderboard.GetEntityScoreAsync(LeaderboardKey, entity.Id);

        Assert.Equal(expectedScore, result);

        _mockRedis.Verify();
    }

    [Fact]
    public async Task GetEntityScoreAsync_WithValidNonExistentEntityKey_ReturnsNull()
    {
        var entityKey = Guid.NewGuid().ToString();

        _mockRedis.Setup(x => x.SortedSetScoreAsync(
                _sortedSetKey,
                entityKey,
                CommandFlags.None))
            .ReturnsAsync((double?)null)
            .Verifiable();

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object, new MemoryPackLeaderboardSerializer());

        var result = await leaderboard.GetEntityScoreAsync(LeaderboardKey, entityKey);

        Assert.Null(result);

        _mockRedis.Verify();
    }

    [Fact]
    public async Task GetEntityScoreAsync_WithInvalidLeaderboardKey_ThrowsArgumentNullException()
    {
        var entity = Player.New();

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object, new MemoryPackLeaderboardSerializer());

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.GetEntityScoreAsync(default, entity.Id));
    }

    [Fact]
    public async Task GetEntityScoreAsync_WithInvalidEntityKey_ThrowsArgumentNullException()
    {
        var leaderboard = new Leaderboard<Player>(_mockRedis.Object, new MemoryPackLeaderboardSerializer());

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.GetEntityScoreAsync(LeaderboardKey, default(string)));
    }

    [Fact]
    public async Task DeleteEntityAsync_WithValidEntity_RemovesEntityFromLeaderboard()
    {
        var entity = Player.New();

        _mockRedis.Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                CommandFlags.None))
            .ReturnsAsync(RedisResult.Create(1))
            .Verifiable();

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object, new MemoryPackLeaderboardSerializer());

        await leaderboard.DeleteEntityAsync(LeaderboardKey, entity.Id);

        _mockRedis.Verify();

        _mockRedis.Verify(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.Is<RedisKey[]>(keys =>
                    keys.Length == 3 &&
                    keys[0] == _sortedSetKey &&
                    keys[1] == _uniqueSortedSetKey &&
                    keys[2] == _hashSetKey),
                It.Is<RedisValue[]>(args => args.Length == 1 && args[0] == entity.Id),
                CommandFlags.None),
            Times.Once);
    }

    [Fact]
    public async Task DeleteEntityAsync_WithInvalidLeaderboardKey_ThrowsArgumentNullException()
    {
        var entityKey = Guid.NewGuid().ToString();

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object, new MemoryPackLeaderboardSerializer());

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.DeleteEntityAsync(default, entityKey));
    }

    [Fact]
    public async Task DeleteEntityAsync_WithInvalidEntityKey_ThrowsArgumentNullException()
    {
        var leaderboard = new Leaderboard<Player>(_mockRedis.Object, new MemoryPackLeaderboardSerializer());

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.DeleteEntityAsync(LeaderboardKey, default(string)));
    }

    [Fact]
    public async Task DeleteEntitiesAsync_WithNullEntityKeys_ThrowsArgumentNullException()
    {
        var leaderboard = new Leaderboard<Player>(_mockRedis.Object, new MemoryPackLeaderboardSerializer());

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.DeleteEntitiesAsync(LeaderboardKey, null!));
    }

    [Fact]
    public async Task DeleteEntitiesAsync_WithEmptyEntityKeys_ThrowsArgumentException()
    {
        var leaderboard = new Leaderboard<Player>(_mockRedis.Object, new MemoryPackLeaderboardSerializer());

        await Assert.ThrowsAsync<ArgumentException>(
            async () => await leaderboard.DeleteEntitiesAsync(LeaderboardKey, []));
    }

    [Fact]
    public async Task DeleteEntitiesAsync_WithValidEntityKeys_ExecutesBatchScript()
    {
        RedisValue[] ids = ["batch_1", "batch_2"];

        _mockRedis.Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                CommandFlags.None))
            .ReturnsAsync(RedisResult.Create(1))
            .Verifiable();

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object, new MemoryPackLeaderboardSerializer());

        await leaderboard.DeleteEntitiesAsync(LeaderboardKey, ids);

        _mockRedis.Verify();
    }

    [Fact]
    public async Task GetSizeAsync_WithValidLeaderboardKey_ReturnsCorrectSize()
    {
        var expectedSize = _random.Next();

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object, new MemoryPackLeaderboardSerializer());

        _mockRedis.Setup(db => db.HashLengthAsync(
                _hashSetKey,
                CommandFlags.None))
            .ReturnsAsync(expectedSize)
            .Verifiable();

        var result = await leaderboard.GetSizeAsync(LeaderboardKey);

        Assert.Equal(expectedSize, result);

        _mockRedis.Verify();
    }

    [Fact]
    public async Task GetSizeAsync_WithInvalidLeaderboardKey_ThrowsArgumentException()
    {
        var leaderboard = new Leaderboard<Player>(_mockRedis.Object, new MemoryPackLeaderboardSerializer());

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.GetSizeAsync(default));
    }

    [Fact]
    public async Task DeleteAsync_ValidLeaderboardKey_CallsKeyDeleteAsyncWithCorrectKeys()
    {
        RedisKey[] expectedKeys =
        {
            _sortedSetKey,
            _hashSetKey,
            _uniqueSortedSetKey
        };

        _mockRedis.Setup(db => db.KeyDeleteAsync(It.IsAny<RedisKey[]>(), CommandFlags.None))
            .ReturnsAsync(3)
            .Verifiable();

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object, new MemoryPackLeaderboardSerializer());

        await leaderboard.DeleteAsync(LeaderboardKey);

        _mockRedis.Verify(
            db => db.KeyDeleteAsync(
                It.Is<RedisKey[]>(keys => keys.SequenceEqual(expectedKeys)),
                CommandFlags.None),
            Times.Once);
    }

    [Fact]
    public async Task GetEntityRankAsync_WithInvalidLeaderboardKey_ThrowsArgumentException()
    {
        var entityKey = Guid.NewGuid().ToString();

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object, new MemoryPackLeaderboardSerializer());

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.GetEntityRankAsync(default, entityKey));
    }

    [Fact]
    public async Task GetEntityRankAsync_WithInvalidEntityKey_ThrowsArgumentException()
    {
        var leaderboard = new Leaderboard<Player>(_mockRedis.Object, new MemoryPackLeaderboardSerializer());

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.GetEntityRankAsync(LeaderboardKey, default(string)));
    }

    [Fact]
    public async Task DeleteAsync_WithInvalidLeaderboardKey_ThrowsArgumentException()
    {
        var leaderboard = new Leaderboard<Player>(_mockRedis.Object, new MemoryPackLeaderboardSerializer());

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.DeleteAsync(default));
    }

    [Fact]
    public async Task GetEntitiesByRankRangeAsync_WithInvalidLeaderboardKey_ThrowsArgumentNullException()
    {
        var leaderboard = new Leaderboard<Player>(_mockRedis.Object, new MemoryPackLeaderboardSerializer());

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.GetEntitiesByRankRangeAsync(default, 1, 10));
    }

    [Fact]
    public async Task GetEntitiesByRankRangeAsync_WithStartRankLessThanOne_ThrowsArgumentOutOfRangeException()
    {
        var leaderboard = new Leaderboard<Player>(_mockRedis.Object, new MemoryPackLeaderboardSerializer());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await leaderboard.GetEntitiesByRankRangeAsync(LeaderboardKey, 0, 10));
    }

    [Fact]
    public async Task GetEntitiesByRankRangeAsync_WithNegativeStartRank_ThrowsArgumentOutOfRangeException()
    {
        var leaderboard = new Leaderboard<Player>(_mockRedis.Object, new MemoryPackLeaderboardSerializer());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await leaderboard.GetEntitiesByRankRangeAsync(LeaderboardKey, -5, 10));
    }

    [Fact]
    public async Task GetEntitiesByRankRangeAsync_WithEndRankLessThanStartRank_ThrowsArgumentOutOfRangeException()
    {
        var leaderboard = new Leaderboard<Player>(_mockRedis.Object, new MemoryPackLeaderboardSerializer());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await leaderboard.GetEntitiesByRankRangeAsync(LeaderboardKey, 10, 5));
    }

    [Fact]
    public async Task GetEntitiesByRankRangeAsync_WithCancelledToken_ThrowsOperationCancelledException()
    {
        var leaderboard = new Leaderboard<Player>(_mockRedis.Object, new MemoryPackLeaderboardSerializer());
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await leaderboard.GetEntitiesByRankRangeAsync(LeaderboardKey, 1, 10, cancellationToken: cts.Token));
    }
}
