using FluentAssertions;
using Redisboard.NET.Enumerations;
using Redisboard.NET.Tests.Common.Models;

namespace Redisboard.NET.Tests.Integration.Redis;

public class LeaderboardIntegrationTests : IClassFixture<LeaderboardFixture>, IDisposable
{
    private readonly LeaderboardFixture _leaderboardFixture;
    private readonly Random _random = new();

    private string LeaderboardKey => _leaderboardFixture.LeaderboardKey;

    public LeaderboardIntegrationTests(LeaderboardFixture leaderboardFixture)
    {
        _leaderboardFixture = leaderboardFixture;
    }

    [Fact]
    public async Task GetEntityAndNeighboursAsync_WithValidDataDefaultRanking_ReturnsLeaderboard()
    {
        var leaderboard = _leaderboardFixture.Instance;
        var expectedRanks = new[] { 1, 2, 3, 4 };

        var entities = new[]
        {
            new TestPlayer { Key = "Mike", Score = 200 },
            new TestPlayer { Key = "Alex", Score = 100 }, 
            new TestPlayer { Key = "John", Score = 100 },
            new TestPlayer { Key = "Sam", Score = 50 },
        };

        await leaderboard.AddEntitiesAsync(LeaderboardKey, entities);

        var result = await leaderboard.GetEntityAndNeighboursAsync(
            LeaderboardKey, "Alex", offset: 10);

        result.Should().NotBeNull();
        result.Should().HaveCount(entities.Length);

        result.First(p => p.Key == "Mike")
            .Rank.Should().Be(expectedRanks[0]);

        result.First(p => p.Key == "Alex")
            .Rank.Should().Be(expectedRanks[1]);

        result.First(p => p.Key == "John")
            .Rank.Should().Be(expectedRanks[2]);
        
        result.First(p => p.Key == "Sam")
            .Rank.Should().Be(expectedRanks[3]);
    }

    [Fact]
    public async Task GetEntityAndNeighboursAsync_WithValidDataDenseRanking_ReturnsLeaderboard()
    {
        var leaderboard = _leaderboardFixture.Instance;
        var expectedRanks = new[] { 1, 2, 3, 3, 4 };

        var entities = new[]
        {
            new TestPlayer { Key = "player1", Score = 250 },
            new TestPlayer { Key = "player2", Score = 200 },
            new TestPlayer { Key = "player3", Score = 100 },
            new TestPlayer { Key = "player4", Score = 100 },
            new TestPlayer { Key = "player5", Score = 50 },
        };
        
        // randomize array
        _random.Shuffle(entities);

        await leaderboard.AddEntitiesAsync(LeaderboardKey, entities);

        var result = await leaderboard.GetEntityAndNeighboursAsync(
            LeaderboardKey, "player2", offset: 10, RankingType.DenseRank);

        result.Should().NotBeNull();
        result.Should().HaveCount(entities.Length);

        result.First(p => p.Key == "player1")
            .Rank.Should().Be(expectedRanks[0]);

        result.First(p => p.Key == "player2")
            .Rank.Should().Be(expectedRanks[1]);

        result.First(p => p.Key == "player3")
            .Rank.Should().Be(expectedRanks[2]);

        result.First(p => p.Key == "player4")
            .Rank.Should().Be(expectedRanks[3]);

        result.First(p => p.Key == "player5")
            .Rank.Should().Be(expectedRanks[4]);
    }

    [Fact]
    public async Task GetEntityAndNeighboursAsync_WithValidDataStandardCompetitionRanking_ReturnsLeaderboard()
    {
        var leaderboard = _leaderboardFixture.Instance;
        var expectedRanks = new[] { 1, 2, 3, 3, 5 };

        var entities = new[]
        {
            new TestPlayer { Key = "player1", Score = 250 },
            new TestPlayer { Key = "player2", Score = 200 },
            new TestPlayer { Key = "player3", Score = 100 },
            new TestPlayer { Key = "player4", Score = 100 },
            new TestPlayer { Key = "player5", Score = 50 },
        };
        
        // randomize array
        _random.Shuffle(entities);

        await leaderboard.AddEntitiesAsync(LeaderboardKey, entities);

        var result = await leaderboard.GetEntityAndNeighboursAsync(
            LeaderboardKey, "player2", offset: 10, RankingType.StandardCompetition);

        result.Should().NotBeNull();
        result.Should().HaveCount(entities.Length);

        result.First(p => p.Key == "player1")
            .Rank.Should().Be(expectedRanks[0]);

        result.First(p => p.Key == "player2")
            .Rank.Should().Be(expectedRanks[1]);

        result.First(p => p.Key == "player3")
            .Rank.Should().Be(expectedRanks[2]);

        result.First(p => p.Key == "player4")
            .Rank.Should().Be(expectedRanks[3]);

        result.First(p => p.Key == "player5")
            .Rank.Should().Be(expectedRanks[4]);
    }

    [Fact]
    public async Task GetEntityAndNeighboursAsync_WithValidDataModifiedCompetitionRanking_ReturnsLeaderboard()
    {
        var leaderboard = _leaderboardFixture.Instance;
        var expectedRanks = new[] { 1, 2, 4, 4, 5 };

        var entities = new[]
        {
            new TestPlayer { Key = "player1", Score = 250 },
            new TestPlayer { Key = "player2", Score = 200 },
            new TestPlayer { Key = "player3", Score = 100 },
            new TestPlayer { Key = "player4", Score = 100 },
            new TestPlayer { Key = "player5", Score = 50 },
        };
        
        // randomize array
        _random.Shuffle(entities);

        await leaderboard.AddEntitiesAsync(LeaderboardKey, entities);

        var result = await leaderboard.GetEntityAndNeighboursAsync(
            LeaderboardKey, "player2", offset: 10, RankingType.ModifiedCompetition);

        result.Should().NotBeNull();
        result.Should().HaveCount(entities.Length);

        result.First(p => p.Key == "player1")
            .Rank.Should().Be(expectedRanks[0]);

        result.First(p => p.Key == "player2")
            .Rank.Should().Be(expectedRanks[1]);

        result.First(p => p.Key == "player3")
            .Rank.Should().Be(expectedRanks[2]);

        result.First(p => p.Key == "player4")
            .Rank.Should().Be(expectedRanks[3]);

        result.First(p => p.Key == "player5")
            .Rank.Should().Be(expectedRanks[4]);
    }

    [Fact]
    public async Task GetEntityAndNeighboursAsync_WithValidDataDefaultRanking_CorrectPageSize_ReturnsLeaderboard() 
        => await TestCorrectPageSizeForGetEntityAndNeighboursAsync(RankingType.Default);

    [Fact]
    public async Task GetEntityAndNeighboursAsync_WithValidDataModifiedCompetitionRanking_CorrectPageSize_ReturnsLeaderboard() 
        => await TestCorrectPageSizeForGetEntityAndNeighboursAsync(RankingType.ModifiedCompetition);

    [Fact]
    public async Task GetEntityAndNeighboursAsync_WithValidDataStandardCompetitionRanking_CorrectPageSize_ReturnsLeaderboard() 
        => await TestCorrectPageSizeForGetEntityAndNeighboursAsync(RankingType.StandardCompetition);

    [Fact]
    public async Task GetEntityAndNeighboursAsync_WithValidDataDenseRankRanking_CorrectPageSize_ReturnsLeaderboard() 
        => await TestCorrectPageSizeForGetEntityAndNeighboursAsync(RankingType.DenseRank);

    private async Task TestCorrectPageSizeForGetEntityAndNeighboursAsync(RankingType rankingType)
    {
        const int maxPlayersCount = 500;
        const int minOffset = 10;
        const int maxOffset = 20;

        var random = new Random();

        var leaderboard = _leaderboardFixture.Instance;

        var offset = random.Next(minOffset, maxOffset);
        var playersCount = random.Next(offset + 1, maxPlayersCount);

        var entities = Enumerable
            .Range(0, playersCount)
            .Select(i => new TestPlayer { Score = i, Key = $"player{i}" })
            .ToArray();

        // get a player to search for
        var playerIndexToSearchFor = random.Next(offset + 1, playersCount - offset);
        var playerKey = entities[playerIndexToSearchFor].Key;

        await leaderboard.AddEntitiesAsync(LeaderboardKey, entities);

        var result = await leaderboard
            .GetEntityAndNeighboursAsync(LeaderboardKey, playerKey, offset, rankingType);

        result.Should().NotBeNull();
        result.Should().HaveCount((offset * 2) + 1); // +1 for the player itself

        result.Should().OnlyContain(r => entities.Any(e => e.Key == r.Key));
    }

    public void Dispose() => _leaderboardFixture?.DeleteLeaderboardAsync();
}