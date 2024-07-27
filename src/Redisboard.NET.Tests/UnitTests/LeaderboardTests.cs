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
    public async Task AddEntitiesAsync_WithInvalidLeaderboardKey_ThrowsArgumentNullException()
    {
        var entity = Player.New();

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.AddEntityAsync(default, entity));
    }

    [Fact]
    public async Task AddEntitiesAsync_WithInvalidEntities_ThrowsArgumentNullException()
    {
        Player entity = null;

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.AddEntityAsync(LeaderboardKey, entity));
    }

    [Fact]
    public async Task AddEntitiesAsync_WithTransactionNotCommitted_ThrowsTransactionException()
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
    public async Task AddEntitiesAsync_WithValidEntities_AddsEntitiesToLeaderboard()
    {
        const bool transactionCommitedStatus = true;
        const double initialScore = 0;

        var entity = Player.New();

        var transactionMock = new Mock<ITransaction>();

        transactionMock.Setup(tr => tr.ExecuteAsync(CommandFlags.None))
            .ReturnsAsync(transactionCommitedStatus)
            .Verifiable();

        _mockRedis.Setup(db => db.CreateTransaction(null))
            .Returns(transactionMock.Object)
            .Verifiable();

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        await leaderboard.AddEntityAsync(
            LeaderboardKey, entity);

        _mockRedis.Verify();

        transactionMock.Verify();

        transactionMock.Verify(db => db.SortedSetAddAsync(
                _sortedSetKey,
                entity.Key,
                initialScore,
                CommandFlags.None),
            Times.Once);

        transactionMock.Verify(db => db.HashSetAsync(
                _hashSetKey,
                entity.Key,
                It.IsAny<RedisValue>(),
                When.Always,
                CommandFlags.None),
            Times.Once);

        transactionMock.Verify(db => db.SortedSetAddAsync(
                _uniqueSortedSetKey,
                entity.Key,
                initialScore,
                CommandFlags.None),
            Times.Once);
    }

    [Fact]
    public async Task GetEntityAndNeighboursAsync_WithInvalidLeaderboardKey_ThrowsArgumentNullException()
    {
        var entity = Guid.NewGuid().ToString();

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.GetEntityAndNeighboursAsync(default, entity));
    }

    [Fact]
    public async Task GetEntityAndNeighboursAsync_WithInvalidEntities_ThrowsArgumentNullException()
    {
        var entity = string.Empty;

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.GetEntityAndNeighboursAsync(LeaderboardKey, entity));
    }

    [Fact]
    public async Task GetEntityAndNeighboursAsync_WithInvalidOffset_ThrowsArgumentOutOfRangeException()
    {
        var entity = Guid.NewGuid().ToString();
        const int invalidOffset = -100;

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await leaderboard.GetEntityAndNeighboursAsync(LeaderboardKey, entity, invalidOffset));
    }

    [Fact]
    public async Task GetEntitiesByScoreRangeAsync_WithInvalidLeaderboardKey_ThrowsArgumentNullException()
    {
        var leaderboardKey = default(RedisValue);

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.GetEntitiesByScoreRangeAsync(leaderboardKey, 0, 100));
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

    // [Fact]
    // public async Task GetEntityDataAsync_WithValidEntityKey_ReturnsEntityData()
    // {
    //     var entityKey = Guid.NewGuid().ToString();
    //     var entity = Player.New();
    //
    //     _mockRedis.Setup(db => db.HashGetAsync(
    //             CacheKey.ForEntityDataHashSet(LeaderboardKey),
    //             entityKey,
    //             CommandFlags.None))
    //         .ReturnsAsync(JsonSerializer.Serialize(entity))
    //         .Verifiable();
    //
    //     var leaderboard = new Leaderboard<Player>(_mockRedis.Object);
    //
    //     var result = await leaderboard.GetEntityDataAsync(LeaderboardKey, entityKey);
    //     
    //     Assert.NotNull(result);
    //     Assert.Equal(entity.Username, result.Username);
    //
    //     _mockRedis.Verify();
    // }
    //
    // [Fact]
    // public async Task GetEntityDataAsync_WithInvalidEntityKey_ReturnsNull()
    // {
    //     const string entityKey = "invalidKey";
    //
    //     _mockRedis.Setup(db => db.HashGetAsync(
    //             CacheKey.ForEntityDataHashSet(LeaderboardKey),
    //             entityKey,
    //             CommandFlags.None))
    //         .ReturnsAsync(RedisValue.Null)
    //         .Verifiable();
    //
    //     var leaderboard = new Leaderboard<Player>(_mockRedis.Object);
    //
    //     var result = await leaderboard.GetEntityDataAsync(LeaderboardKey, entityKey);
    //
    //     Assert.Null(result);
    //
    //     _mockRedis.Verify();
    // }

    [Fact]
    public void GetEntityDataAsync_WithInvalidLeaderboardKey_ThrowsArgumentNullException()
    {
        var leaderboardKey = default(RedisValue);
        var entityKey = Guid.NewGuid().ToString();

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.GetEntityMetadataAsync(leaderboardKey, entityKey));
    }

    [Fact]
    public void GetEntityDataAsync_WithInvalidEntityKey_ThrowsArgumentNullException()
    {
        var leaderboardKey = Guid.NewGuid().ToString();
        const string entityKey = default;

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.GetEntityMetadataAsync(leaderboardKey, entityKey));
    }

    [Fact]
    public async Task GetEntityScoreAsync_WithValidEntityKey_ReturnsScore()
    {
        var entityKey = Guid.NewGuid().ToString();
        var expectedScore = _random.Next(20, 500);

        _mockRedis.Setup(x => x.SortedSetScoreAsync(
                CacheKey.ForLeaderboardSortedSet(LeaderboardKey),
                entityKey,
                CommandFlags.None))
            .ReturnsAsync(expectedScore)
            .Verifiable();

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        var result = await leaderboard.GetEntityScoreAsync(LeaderboardKey, entityKey);

        Assert.Equal(expectedScore, result);

        _mockRedis.Verify();
    }

    [Fact]
    public async Task GetEntityScoreAsync_WithValidNonExistentEntityKey_ReturnsNull()
    {
        var entityKey = Guid.NewGuid().ToString();
        double? expectedScore = null;

        _mockRedis.Setup(x => x.SortedSetScoreAsync(
                CacheKey.ForLeaderboardSortedSet(LeaderboardKey),
                entityKey,
                CommandFlags.None))
            .ReturnsAsync(expectedScore)
            .Verifiable();

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        var result = await leaderboard.GetEntityScoreAsync(LeaderboardKey, entityKey);

        Assert.Equal(expectedScore, result);

        _mockRedis.Verify();
    }

    [Fact]
    public async Task GetEntityScoreAsync_WithInvalidLeaderboardKey_ThrowsArgumentNullException()
    {
        RedisValue leaderboardKey = default;
        var entityKey = Guid.NewGuid().ToString();

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.GetEntityScoreAsync(leaderboardKey, entityKey));
    }

    [Fact]
    public async Task GetEntityScoreAsync_WithInvalidEntityKey_ThrowsArgumentNullException()
    {
        string entityKey = null;

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.GetEntityScoreAsync(LeaderboardKey, entityKey));
    }

    [Fact]
    public async Task DeleteEntityAsync_WithValidEntityKey_ThrowsArgumentNullException()
    {
        var entityKey = Guid.NewGuid().ToString();
        const bool transactionCommitedStatus = true;

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);
        
        var transactionMock = new Mock<ITransaction>();

        _mockRedis
            .Setup(db => db.CreateTransaction(null))
            .Returns(transactionMock.Object)
            .Verifiable();

        transactionMock.Setup(tr => tr.ExecuteAsync(CommandFlags.None))
            .ReturnsAsync(transactionCommitedStatus)
            .Verifiable();

        await leaderboard.DeleteEntityAsync(LeaderboardKey, entityKey);

        _mockRedis.Verify();
        
        transactionMock.Verify();
        
        transactionMock.Verify(db => db.SortedSetRemoveAsync(
            CacheKey.ForLeaderboardSortedSet(LeaderboardKey),
            entityKey,
            CommandFlags.None), Times.Once);

        transactionMock.Verify(db => db.HashDeleteAsync(
            CacheKey.ForEntityDataHashSet(LeaderboardKey),
            entityKey,
            CommandFlags.None), Times.Once);

        transactionMock.Verify(db => db.SortedSetRemoveAsync(
            CacheKey.ForUniqueScoreSortedSet(LeaderboardKey),
            entityKey,
            CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task DeleteEntityAsync_WithInvalidLeaderboardKey_ThrowsArgumentNullException()
    {
        RedisValue leaderboardKey = default;
        var entityKey = Guid.NewGuid().ToString();

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.DeleteEntityAsync(leaderboardKey, entityKey));
    }

    [Fact]
    public async Task DeleteEntityAsync_WithInvalidEntityKey_ThrowsArgumentNullException()
    {
        string entityKey = null;

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.DeleteEntityAsync(LeaderboardKey, entityKey));
    }

    [Fact]
    public async Task GetSizeAsync_WithValidLeaderboardKey_ReturnsCorrectSize()
    {
        var expectedSize = _random.Next();

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        _mockRedis.Setup(db => db.HashLengthAsync(
                CacheKey.ForEntityDataHashSet(LeaderboardKey),
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
        const string leaderboardKey = default;

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.GetSizeAsync(leaderboardKey));
    }

    [Fact]
    public async Task DeleteAsync_ValidLeaderboardKey_CallsKeyDeleteAsyncWithCorrectKeys()
    {
        RedisKey[] expectedKeys =
        {
            CacheKey.ForLeaderboardSortedSet(LeaderboardKey),
            CacheKey.ForEntityDataHashSet(LeaderboardKey),
            CacheKey.ForUniqueScoreSortedSet(LeaderboardKey)
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
        const string leaderboardKey = default;
        var entityKey = Guid.NewGuid().ToString();

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.GetEntityRankAsync(leaderboardKey, entityKey)); 
    }

    [Fact]
    public async Task GetEntityRankAsync_WithInvalidEntityKey_ThrowsArgumentException()
    {
        const string entityKey = default;

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);
        
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.GetEntityRankAsync(LeaderboardKey, entityKey));
    }
    
    [Fact]
    public async Task DeleteAsync_WithInvalidLeaderboardKey_ThrowsArgumentException()
    {
        const string leaderboardKey = default;

        var leaderboard = new Leaderboard<Player>(_mockRedis.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await leaderboard.DeleteAsync(leaderboardKey));
    }
}