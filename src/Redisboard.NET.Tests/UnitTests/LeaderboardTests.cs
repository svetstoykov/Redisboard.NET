using System.Text.Json;
using System.Transactions;
using Moq;
using Redisboard.NET.Common.Models;
using Redisboard.NET.Helpers;
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

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.AddEntityAsync(default, entity));
    }

    [Fact]
    public async Task AddEntityAsync_WithTransactionNotCommitted_ThrowsTransactionException()
    {
        const bool transactionCommitedStatus = false;

        var entity = Player.New();

        var transactionMock = new Mock<ITransaction>();

        transactionMock.Setup(tr => tr.ExecuteAsync(CommandFlags.None))
            .ReturnsAsync(transactionCommitedStatus)
            .Verifiable();

        _mockRedis.Setup(db => db.CreateTransaction(null))
            .Returns(transactionMock.Object)
            .Verifiable();

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        await Assert.ThrowsAsync<TransactionException>(
            async () => await leaderboard.AddEntityAsync(LeaderboardKey, entity));
    }

    [Fact]
    public async Task AddEntityAsync_WithValidEntity_AddsEntityToLeaderboard()
    {
        const bool transactionCommitedStatus = true;

        var entity = Player.New();
        var invertedScore = -entity.Score;

        var transactionMock = new Mock<ITransaction>();

        transactionMock.Setup(tr => tr.ExecuteAsync(CommandFlags.None))
            .ReturnsAsync(transactionCommitedStatus)
            .Verifiable();

        _mockRedis.Setup(db => db.CreateTransaction(null))
            .Returns(transactionMock.Object)
            .Verifiable();

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        await leaderboard.AddEntityAsync(LeaderboardKey, entity);

        _mockRedis.Verify();
        transactionMock.Verify();

        transactionMock.Verify(db => db.SortedSetAddAsync(
                _sortedSetKey,
                entity.Id,
                invertedScore,
                CommandFlags.None),
            Times.Once);

        transactionMock.Verify(db => db.HashSetAsync(
                _hashSetKey,
                entity.Id,
                It.IsAny<RedisValue>(),
                When.Always,
                CommandFlags.None),
            Times.Once);

        transactionMock.Verify(db => db.SortedSetAddAsync(
                _uniqueSortedSetKey,
                invertedScore,
                invertedScore,
                CommandFlags.None),
            Times.Once);
    }

    [Fact]
    public async Task UpdateEntityScoreAsync_WithInvalidLeaderboardKey_ThrowsArgumentNullException()
    {
        var entity = Player.New();

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

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
            .ReturnsAsync(RedisResult.Create((RedisValue[])Array.Empty<RedisValue>()))
            .Verifiable();

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        await leaderboard.UpdateEntityScoreAsync(LeaderboardKey, entity);

        _mockRedis.Verify();
    }

    [Fact]
    public async Task UpdateEntityMetadataAsync_WithInvalidLeaderboardKey_ThrowsArgumentNullException()
    {
        var entity = Player.New();

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

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

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        await leaderboard.UpdateEntityMetadataAsync(LeaderboardKey, entity);

        _mockRedis.Verify();
    }

    [Fact]
    public async Task GetEntityAndNeighboursAsync_WithInvalidLeaderboardKey_ThrowsArgumentNullException()
    {
        var entity = Player.New();

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.GetEntityAndNeighboursAsync(default, entity.Id));
    }

    [Fact]
    public async Task GetEntityAndNeighboursAsync_WithInvalidEntityKey_ThrowsArgumentNullException()
    {
        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.GetEntityAndNeighboursAsync(LeaderboardKey, default(string)));
    }

    [Fact]
    public async Task GetEntityAndNeighboursAsync_WithInvalidOffset_ThrowsArgumentOutOfRangeException()
    {
        var entity = Player.New();
        const int invalidOffset = -100;

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await leaderboard.GetEntityAndNeighboursAsync(LeaderboardKey, entity.Id, invalidOffset));
    }

    [Fact]
    public async Task GetEntitiesByScoreRangeAsync_WithInvalidLeaderboardKey_ThrowsArgumentNullException()
    {
        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.GetEntitiesByScoreRangeAsync(default, 0, 100));
    }

    [Fact]
    public async Task GetEntitiesByScoreRangeAsync_WithInvalidMinScore_ThrowsArgumentException()
    {
        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await leaderboard.GetEntitiesByScoreRangeAsync(LeaderboardKey, -100, 100));
    }

    [Fact]
    public async Task GetEntitiesByScoreRangeAsync_WithInvalidMaxScore_ThrowsArgumentException()
    {
        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await leaderboard.GetEntitiesByScoreRangeAsync(LeaderboardKey, 0, -100));
    }

    [Fact]
    public async Task GetEntitiesByScoreRangeAsync_WithInvalidScoreRange_ThrowsInvalidOperationException()
    {
        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

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

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

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

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        var result = await leaderboard.GetEntityScoreAsync(LeaderboardKey, entityKey);

        Assert.Null(result);

        _mockRedis.Verify();
    }

    [Fact]
    public async Task GetEntityScoreAsync_WithInvalidLeaderboardKey_ThrowsArgumentNullException()
    {
        var entity = Player.New();

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.GetEntityScoreAsync(default, entity.Id));
    }

    [Fact]
    public async Task GetEntityScoreAsync_WithInvalidEntityKey_ThrowsArgumentNullException()
    {
        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.GetEntityScoreAsync(LeaderboardKey, default(string)));
    }

    [Fact]
    public async Task DeleteEntityAsync_WithValidEntity_RemovesEntityFromLeaderboard()
    {
        const bool transactionCommitedStatus = true;
        var entity = Player.New();

        var transactionMock = new Mock<ITransaction>();

        _mockRedis
            .Setup(db => db.CreateTransaction(null))
            .Returns(transactionMock.Object)
            .Verifiable();

        transactionMock.Setup(tr => tr.ExecuteAsync(CommandFlags.None))
            .ReturnsAsync(transactionCommitedStatus)
            .Verifiable();

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        await leaderboard.DeleteEntityAsync(LeaderboardKey, entity.Id);

        _mockRedis.Verify();
        transactionMock.Verify();

        transactionMock.Verify(db => db.SortedSetRemoveAsync(
            _sortedSetKey,
            entity.Id,
            CommandFlags.None), Times.Once);

        transactionMock.Verify(db => db.HashDeleteAsync(
            _hashSetKey,
            entity.Id,
            CommandFlags.None), Times.Once);

        transactionMock.Verify(db => db.SortedSetRemoveAsync(
            _uniqueSortedSetKey,
            entity.Id,
            CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task DeleteEntityAsync_WithInvalidLeaderboardKey_ThrowsArgumentNullException()
    {
        var entityKey = Guid.NewGuid().ToString();

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.DeleteEntityAsync(default, entityKey));
    }

    [Fact]
    public async Task DeleteEntityAsync_WithInvalidEntityKey_ThrowsArgumentNullException()
    {
        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.DeleteEntityAsync(LeaderboardKey, default(string)));
    }

    [Fact]
    public async Task GetSizeAsync_WithValidLeaderboardKey_ReturnsCorrectSize()
    {
        var expectedSize = _random.Next();

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

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
        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

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

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

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

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.GetEntityRankAsync(default, entityKey));
    }

    [Fact]
    public async Task GetEntityRankAsync_WithInvalidEntityKey_ThrowsArgumentException()
    {
        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.GetEntityRankAsync(LeaderboardKey, default(string)));
    }

    [Fact]
    public async Task DeleteAsync_WithInvalidLeaderboardKey_ThrowsArgumentException()
    {
        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.DeleteAsync(default));
    }

    [Fact]
    public async Task GetEntitiesByRankRangeAsync_WithInvalidLeaderboardKey_ThrowsArgumentNullException()
    {
        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.GetEntitiesByRankRangeAsync(default, 1, 10));
    }

    [Fact]
    public async Task GetEntitiesByRankRangeAsync_WithStartRankLessThanOne_ThrowsArgumentOutOfRangeException()
    {
        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await leaderboard.GetEntitiesByRankRangeAsync(LeaderboardKey, 0, 10));
    }

    [Fact]
    public async Task GetEntitiesByRankRangeAsync_WithNegativeStartRank_ThrowsArgumentOutOfRangeException()
    {
        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await leaderboard.GetEntitiesByRankRangeAsync(LeaderboardKey, -5, 10));
    }

    [Fact]
    public async Task GetEntitiesByRankRangeAsync_WithEndRankLessThanStartRank_ThrowsArgumentOutOfRangeException()
    {
        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await leaderboard.GetEntitiesByRankRangeAsync(LeaderboardKey, 10, 5));
    }

    [Fact]
    public async Task GetEntitiesByRankRangeAsync_WithCancelledToken_ThrowsOperationCancelledException()
    {
        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await leaderboard.GetEntitiesByRankRangeAsync(LeaderboardKey, 1, 10, cancellationToken: cts.Token));
    }
}