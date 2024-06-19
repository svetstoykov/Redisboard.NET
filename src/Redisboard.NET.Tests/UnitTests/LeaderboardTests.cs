using Moq;
using Redisboard.NET.Helpers;
using Redisboard.NET.Tests.Common.Models;
using StackExchange.Redis;

namespace Redisboard.NET.Tests.UnitTests;

public class LeaderboardTests
{
    private const string LeaderboardId = nameof(LeaderboardId);

    private readonly Mock<IDatabase> _mockDatabase;

    public LeaderboardTests()
    {
        _mockDatabase = new Mock<IDatabase>();
    }

    [Fact]
    public async Task AddEntitiesAsync_WithSingleValidEntity_AddsEntityToLeaderboard()
    {
        var entity = TestPlayer.New();

        var leaderboard = new Leaderboard<TestPlayer>(_mockDatabase.Object);

        await leaderboard.AddEntitiesAsync(
            LeaderboardId, [entity]);

        _mockDatabase.Verify(db => db.SortedSetAddAsync(
                CacheKey.ForLeaderboardSortedSet(LeaderboardId),
                It.Is<SortedSetEntry[]>(e => e.Length == 1),
                CommandFlags.None),
            Times.Once);

        _mockDatabase.Verify(db => db.HashSetAsync(
                CacheKey.ForEntityDataHashSet(LeaderboardId),
                It.Is<HashEntry[]>(e => e.Length == 1),
                CommandFlags.None),
            Times.Once);

        _mockDatabase.Verify(db => db.SortedSetAddAsync(
                CacheKey.ForUniqueScoreSortedSet(LeaderboardId),
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
                CacheKey.ForLeaderboardSortedSet(LeaderboardId),
                It.Is<SortedSetEntry[]>(e => e.Length == count),
                CommandFlags.None),
            Times.Once);

        _mockDatabase.Verify(db => db.HashSetAsync(
                CacheKey.ForEntityDataHashSet(LeaderboardId),
                It.Is<HashEntry[]>(e => e.Length == count),
                CommandFlags.None),
            Times.Once);

        _mockDatabase.Verify(db => db.SortedSetAddAsync(
                CacheKey.ForUniqueScoreSortedSet(LeaderboardId),
                It.Is<SortedSetEntry[]>(e => e.Length == count),
                CommandFlags.None),
            Times.Once);
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
    public async Task AddEntitiesAsync_WithInvalidLeaderboardKey_ThrowsArgumentNullException()
    {
        var entities = new TestPlayer[5];

        var leaderboard = new Leaderboard<TestPlayer>(_mockDatabase.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await leaderboard.AddEntitiesAsync(null, entities));
    }
}