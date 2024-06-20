using System.Reflection.Metadata;
using AutoFixture;
using Moq;
using Redisboard.NET.Helpers;
using Redisboard.NET.Tests.Common.Models;
using StackExchange.Redis;

namespace Redisboard.NET.Tests.UnitTests;

public class LeaderboardTests
{
    private const string LeaderboardId = nameof(LeaderboardId);

    private readonly string _sortedSetKey = CacheKey.ForLeaderboardSortedSet(LeaderboardId);
    private readonly string _hashSetKey = CacheKey.ForEntityDataHashSet(LeaderboardId);
    private readonly string _uniqueSortedSetKey = CacheKey.ForUniqueScoreSortedSet(LeaderboardId);

    private readonly Mock<IDatabase> _mockDatabase = new();
    private readonly Random _random = new();

    [Fact]
    public async Task AddEntitiesAsync_WithInvalidLeaderboardKey_ThrowsArgumentNullException()
    {
        var entities = new TestPlayer[5];

        var leaderboard = new Leaderboard<TestPlayer>(_mockDatabase.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await leaderboard.AddEntitiesAsync(null, entities));
    }

    [Fact]
    public async Task AddEntitiesAsync_WithInvalidEntities_ThrowsArgumentNullException()
    {
        TestPlayer[] entities = null;

        var leaderboard = new Leaderboard<TestPlayer>(_mockDatabase.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await leaderboard.AddEntitiesAsync(LeaderboardId, entities));
    }

    [Fact]
    public async Task AddEntitiesAsync_WithSingleValidEntity_AddsEntityToLeaderboard()
    {
        var entity = TestPlayer.New();

        var leaderboard = new Leaderboard<TestPlayer>(_mockDatabase.Object);

        await leaderboard.AddEntitiesAsync(
            LeaderboardId, [entity]);

        _mockDatabase.Verify(db => db.SortedSetAddAsync(
                _sortedSetKey,
                It.Is<SortedSetEntry[]>(e => e.Length == 1),
                CommandFlags.None),
            Times.Once);

        _mockDatabase.Verify(db => db.HashSetAsync(
                _hashSetKey,
                It.Is<HashEntry[]>(e => e.Length == 1),
                CommandFlags.None),
            Times.Once);

        _mockDatabase.Verify(db => db.SortedSetAddAsync(
                _uniqueSortedSetKey,
                It.Is<SortedSetEntry[]>(e => e.Length == 1),
                CommandFlags.None),
            Times.Once);
    }

    [Fact]
    public async Task AddEntitiesAsync_WithMultipleValidEntity_AddsEntitiesToLeaderboard()
    {
        const int count = 5;

        var entities = new List<TestPlayer>();
        for (var i = 0; i < count; i++)
        {
            entities.Add(TestPlayer.New());
        }

        var leaderboard = new Leaderboard<TestPlayer>(_mockDatabase.Object);

        await leaderboard.AddEntitiesAsync(
            LeaderboardId, entities.ToArray());

        _mockDatabase.Verify(db => db.SortedSetAddAsync(
                _sortedSetKey,
                It.Is<SortedSetEntry[]>(e => e.Length == count),
                CommandFlags.None),
            Times.Once);

        _mockDatabase.Verify(db => db.HashSetAsync(
                _hashSetKey,
                It.Is<HashEntry[]>(e => e.Length == count),
                CommandFlags.None),
            Times.Once);

        _mockDatabase.Verify(db => db.SortedSetAddAsync(
                _uniqueSortedSetKey,
                It.Is<SortedSetEntry[]>(e => e.Length == count),
                CommandFlags.None),
            Times.Once);
    }

    [Fact]
    public async Task GetEntityAndNeighboursAsync_WithInvalidLeaderboardKey_ThrowsArgumentNullException()
    {
        var entity = Guid.NewGuid().ToString();

        var leaderboard = new Leaderboard<TestPlayer>(_mockDatabase.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await leaderboard.GetEntityAndNeighboursAsync(null, entity));
    }

    [Fact]
    public async Task GetEntityAndNeighboursAsync_WithInvalidEntities_ThrowsArgumentNullException()
    {
        var entity = string.Empty;

        var leaderboard = new Leaderboard<TestPlayer>(_mockDatabase.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await leaderboard.GetEntityAndNeighboursAsync(LeaderboardId, entity));
    }

    [Fact]
    public async Task GetEntityAndNeighboursAsync_WithInvalidOffset_ThrowsArgumentOutOfRangeException()
    {
        var entity = Guid.NewGuid().ToString();
        const int invalidOffset = -100;

        var leaderboard = new Leaderboard<TestPlayer>(_mockDatabase.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await leaderboard.GetEntityAndNeighboursAsync(LeaderboardId, entity, invalidOffset));
    }

    [Fact]
    public async Task GetEntityAndNeighboursAsync_WithValidDataDefaultRanking_ReturnsLeaderboard()
    {
        var entityKey = Guid.NewGuid().ToString();
        var offset = _random.Next(10, 50);
        var playerIndex = _random.NextInt64(50, long.MaxValue);
        
        var startIndex = playerIndex;
        var pageSize = offset * 2;
        var endIndex = playerIndex + pageSize;
        var entities = new Fixture()
            .CreateMany<RedisValue>(pageSize)
            .ToArray();

        _mockDatabase.Setup(db => db.SortedSetRankAsync(
                _sortedSetKey, entityKey, Order.Descending, CommandFlags.None))
            .ReturnsAsync(playerIndex);

        _mockDatabase.Setup(db => db.SortedSetRangeByRankAsync(
                _sortedSetKey, startIndex, endIndex, Order.Descending, CommandFlags.None))
            .ReturnsAsync(entities);
        
        
    }
}