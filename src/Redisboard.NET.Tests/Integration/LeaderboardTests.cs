using System.Text.Json;
using FluentAssertions;
using Redisboard.NET.Common.Models;
using Redisboard.NET.Enumerations;

namespace Redisboard.NET.Tests.Integration;

public class LeaderboardTests : IClassFixture<LeaderboardFixture>, IDisposable
{
    private readonly LeaderboardFixture _leaderboardFixture;
    private readonly Random _random = new();

    private string LeaderboardKey => _leaderboardFixture.LeaderboardKey;

    public LeaderboardTests(LeaderboardFixture leaderboardFixture)
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
            new Player { Key = "Mike", Score = 200 },
            new Player { Key = "Alex", Score = 100 },
            new Player { Key = "John", Score = 100 },
            new Player { Key = "Sam", Score = 50 },
        };

        _random.Shuffle(entities);
        
        foreach (var entity in entities)
        {
            await leaderboard.AddEntityAsync(LeaderboardKey, entity.Key);

            await leaderboard.UpdateEntityScoreAsync(LeaderboardKey, entity.Key, entity.Score);
        }

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
    public async Task GetEntityAndNeighboursAsync_WithValidDataDefaultRanking_Middle_CorrectOffset_ReturnsLeaderboard()
    {
        var leaderboard = _leaderboardFixture.Instance;
        const int offset = 2;
        const int expectedOffset = offset * 2 + 1;

        var entities = new[]
        {
            new Player { Key = "Mike", Score = 200 },
            new Player { Key = "Alex", Score = 100 },
            new Player { Key = "John", Score = 100 },
            new Player { Key = "Sam", Score = 55 },
            new Player { Key = "Jim", Score = 50 },
            new Player { Key = "Dodo", Score = 40 },
            new Player { Key = "Frodo", Score = 30 },
        };

        _random.Shuffle(entities);
        
        foreach (var entity in entities)
        {
            await leaderboard.AddEntityAsync(LeaderboardKey, entity.Key);

            await leaderboard.UpdateEntityScoreAsync(LeaderboardKey, entity.Key, entity.Score);
        }

        var result = await leaderboard.GetEntityAndNeighboursAsync(
            LeaderboardKey, "John", offset: offset, RankingType.DenseRank);

        Assert.Equal(expectedOffset, result.Length);
    }
    
    [Fact]
    public async Task GetEntityAndNeighboursAsync_WithValidDataDenseRanking_Middle_CorrectOffset_ReturnsLeaderboard()
    {
        var leaderboard = _leaderboardFixture.Instance;
        const int offset = 2;
        const int expectedOffset = offset * 2 + 1;

        var entities = new[]
        {
            new Player { Key = "Mike", Score = 200 },
            new Player { Key = "Alex", Score = 100 },
            new Player { Key = "John", Score = 100 },
            new Player { Key = "Sam", Score = 55 },
            new Player { Key = "Jim", Score = 50 },
            new Player { Key = "Dodo", Score = 40 },
            new Player { Key = "Frodo", Score = 30 },
        };

        _random.Shuffle(entities);
        
        foreach (var entity in entities)
        {
            await leaderboard.AddEntityAsync(LeaderboardKey, entity.Key);

            await leaderboard.UpdateEntityScoreAsync(LeaderboardKey, entity.Key, entity.Score);
        }

        var result = await leaderboard.GetEntityAndNeighboursAsync(
            LeaderboardKey, "John", offset: offset);

        Assert.Equal(expectedOffset, result.Length);
    }
    
    [Fact]
    public async Task GetEntityAndNeighboursAsync_WithValidDataCompetitionRanking_Middle_CorrectOffset_ReturnsLeaderboard()
    {
        var leaderboard = _leaderboardFixture.Instance;
        const int offset = 2;
        const int expectedOffset = offset * 2 + 1;

        var entities = new[]
        {
            new Player { Key = "Mike", Score = 200 },
            new Player { Key = "Alex", Score = 100 },
            new Player { Key = "John", Score = 100 },
            new Player { Key = "Sam", Score = 55 },
            new Player { Key = "Jim", Score = 50 },
            new Player { Key = "Dodo", Score = 40 },
            new Player { Key = "Frodo", Score = 30 },
        };

        _random.Shuffle(entities);
        
        foreach (var entity in entities)
        {
            await leaderboard.AddEntityAsync(LeaderboardKey, entity.Key);

            await leaderboard.UpdateEntityScoreAsync(LeaderboardKey, entity.Key, entity.Score);
        }

        var result = await leaderboard.GetEntityAndNeighboursAsync(
            LeaderboardKey, "John", offset: 2, RankingType.StandardCompetition);

        Assert.Equal(expectedOffset, result.Length);
    }
    
    [Fact]
    public async Task GetEntityAndNeighboursAsync_WithValidDataDefaultRanking_FirstPlace_CorrectOffset_ReturnsLeaderboard()
    {
        var leaderboard = _leaderboardFixture.Instance;
        const int offset = 2;

        var entities = new[]
        {
            new Player { Key = "Mike", Score = 200 },
            new Player { Key = "Alex", Score = 100 },
            new Player { Key = "John", Score = 100 },
            new Player { Key = "Sam", Score = 55 },
            new Player { Key = "Jim", Score = 50 },
            new Player { Key = "Dodo", Score = 40 },
            new Player { Key = "Frodo", Score = 30 },
        };

        _random.Shuffle(entities);
        
        foreach (var entity in entities)
        {
            await leaderboard.AddEntityAsync(LeaderboardKey, entity.Key);

            await leaderboard.UpdateEntityScoreAsync(LeaderboardKey, entity.Key, entity.Score);
        }

        var result = await leaderboard.GetEntityAndNeighboursAsync(
            LeaderboardKey, "Mike", offset: 2, RankingType.DenseRank);

        Assert.Equal(offset + 1, result.Length);
    }
    
    [Fact]
    public async Task GetEntityAndNeighboursAsync_WithValidDataDenseRanking_FirstPlace_CorrectOffset_ReturnsLeaderboard()
    {
        var leaderboard = _leaderboardFixture.Instance;
        const int offset = 2;

        var entities = new[]
        {
            new Player { Key = "Mike", Score = 200 },
            new Player { Key = "Alex", Score = 100 },
            new Player { Key = "John", Score = 100 },
            new Player { Key = "Sam", Score = 55 },
            new Player { Key = "Jim", Score = 50 },
            new Player { Key = "Dodo", Score = 40 },
            new Player { Key = "Frodo", Score = 30 },
        };

        _random.Shuffle(entities);
        
        foreach (var entity in entities)
        {
            await leaderboard.AddEntityAsync(LeaderboardKey, entity.Key);

            await leaderboard.UpdateEntityScoreAsync(LeaderboardKey, entity.Key, entity.Score);
        }

        var result = await leaderboard.GetEntityAndNeighboursAsync(
            LeaderboardKey, "Mike", offset: 2);

        Assert.Equal(offset + 1, result.Length);
    }
    
    [Fact]
    public async Task GetEntityAndNeighboursAsync_WithValidDataCompetitionRanking_FirstPlace_CorrectOffset_ReturnsLeaderboard()
    {
        var leaderboard = _leaderboardFixture.Instance;
        const int offset = 2;

        var entities = new[]
        {
            new Player { Key = "Mike", Score = 200 },
            new Player { Key = "Alex", Score = 100 },
            new Player { Key = "John", Score = 100 },
            new Player { Key = "Sam", Score = 55 },
            new Player { Key = "Jim", Score = 50 },
            new Player { Key = "Dodo", Score = 40 },
            new Player { Key = "Frodo", Score = 30 },
        };

        _random.Shuffle(entities);
        
        foreach (var entity in entities)
        {
            await leaderboard.AddEntityAsync(LeaderboardKey, entity.Key);

            await leaderboard.UpdateEntityScoreAsync(LeaderboardKey, entity.Key, entity.Score);
        }

        var result = await leaderboard.GetEntityAndNeighboursAsync(
            LeaderboardKey, "Mike", offset: 2, RankingType.StandardCompetition);

        Assert.Equal(offset + 1, result.Length);
    }
    
    
    [Fact]
    public async Task GetEntityAndNeighboursAsync_WithValidDataDenseRanking_ReturnsLeaderboard()
    {
        var leaderboard = _leaderboardFixture.Instance;
        var expectedRanks = new[] { 1, 2, 3, 3, 4 };

        var entities = new[]
        {
            new Player { Key = "player1", Score = 250 },
            new Player { Key = "player2", Score = 200 },
            new Player { Key = "player3", Score = 100 },
            new Player { Key = "player4", Score = 100 },
            new Player { Key = "player5", Score = 50 },
        };

        // randomize array
        _random.Shuffle(entities);
        
        foreach (var entity in entities)
        {
            await leaderboard.AddEntityAsync(LeaderboardKey, entity.Key);

            await leaderboard.UpdateEntityScoreAsync(LeaderboardKey, entity.Key, entity.Score);
        }
        
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
            new Player { Key = "player1", Score = 250 },
            new Player { Key = "player2", Score = 200 },
            new Player { Key = "player3", Score = 100 },
            new Player { Key = "player4", Score = 100 },
            new Player { Key = "player5", Score = 50 },
        };

        // randomize array
        _random.Shuffle(entities);
        
        foreach (var entity in entities)
        {
            await leaderboard.AddEntityAsync(LeaderboardKey, entity.Key);

            await leaderboard.UpdateEntityScoreAsync(LeaderboardKey, entity.Key, entity.Score);
        }
        
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
            new Player { Key = "player1", Score = 250 },
            new Player { Key = "player2", Score = 200 },
            new Player { Key = "player3", Score = 100 },
            new Player { Key = "player4", Score = 100 },
            new Player { Key = "player5", Score = 50 },
        };

        // randomize array
        _random.Shuffle(entities);

        foreach (var entity in entities)
        {
            await leaderboard.AddEntityAsync(LeaderboardKey, entity.Key);

            await leaderboard.UpdateEntityScoreAsync(LeaderboardKey, entity.Key, entity.Score);
        }
        
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
    public async Task GetEntitiesByScoreRangeAsync_WithValidData_ReturnsLeaderboard()
    {
        var leaderboard = _leaderboardFixture.Instance;
        const double minScore = 50;
        const double maxScore = 100;

        var entities = new[]
        {
            new Player { Key = "Mike", Score = 200 },
            new Player { Key = "Alex", Score = 100 },
            new Player { Key = "John", Score = 100 },
            new Player { Key = "Sam", Score = 50 },
            new Player { Key = "Jim", Score = 20 },
        };

        // randomize array
        _random.Shuffle(entities);

        foreach (var entity in entities)
        {
            await leaderboard.AddEntityAsync(LeaderboardKey, entity.Key);

            await leaderboard.UpdateEntityScoreAsync(LeaderboardKey, entity.Key, entity.Score);
        }

        var result = await leaderboard.GetEntitiesByScoreRangeAsync(
            LeaderboardKey, minScore, maxScore);

        result.Should().NotBeNull();
        result.Should().HaveCount(3);

        Assert.True(entities
            .Where(e => e.Score is >= 50 and <= 100)
            .All(e => result.Any(r => r.Key == e.Key)));
    }

    [Fact]
    public async Task GetEntitiesByScoreRangeAsync_WithOutOfRangeValidData_ReturnsEmptyLeaderboard()
    {
        var leaderboard = _leaderboardFixture.Instance;
        const double minScore = 205;
        const double maxScore = 300;

        var entities = new[]
        {
            new Player { Key = "Mike", Score = 200 },
            new Player { Key = "Alex", Score = 100 },
            new Player { Key = "John", Score = 100 },
            new Player { Key = "Sam", Score = 50 },
            new Player { Key = "Jim", Score = 20 },
        };

        // randomize array
        _random.Shuffle(entities);

        foreach (var entity in entities)
        {
            await leaderboard.AddEntityAsync(LeaderboardKey, entity.Key);

            await leaderboard.UpdateEntityScoreAsync(LeaderboardKey, entity.Key, entity.Score);
        }

        var result = await leaderboard.GetEntitiesByScoreRangeAsync(
            LeaderboardKey, minScore, maxScore);

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEntityRankAsync_WithValidDataDefaultRank_ReturnsRank()
    {
        var leaderboard = _leaderboardFixture.Instance;
        const string entityKey = "John";
        const int expectedRank = 3;

        var entities = new[]
        {
            new Player { Key = "Mike", Score = 200 },
            new Player { Key = "Alex", Score = 100 },
            new Player { Key = "John", Score = 100 },
            new Player { Key = "Sam", Score = 50 },
        };

        // randomize array
        _random.Shuffle(entities);

        foreach (var entity in entities)
        {
            await leaderboard.AddEntityAsync(LeaderboardKey, entity.Key);

            await leaderboard.UpdateEntityScoreAsync(LeaderboardKey, entity.Key, entity.Score);
        }

        var result = await leaderboard.GetEntityRankAsync(LeaderboardKey, entityKey);

        result.Should().NotBeNull();
        result.Value.Should().Be(expectedRank);
    }

    [Fact]
    public async Task GetEntityRankAsync_WithValidDataDenseRank_ReturnsRank()
    {
        var leaderboard = _leaderboardFixture.Instance;
        const string entityKey = "player5";
        const int expectedRank = 4;

        var entities = new[]
        {
            new Player { Key = "player1", Score = 250 },
            new Player { Key = "player2", Score = 200 },
            new Player { Key = "player3", Score = 100 },
            new Player { Key = "player4", Score = 100 },
            new Player { Key = "player5", Score = 50 },
        };

        // randomize array
        _random.Shuffle(entities);

        foreach (var entity in entities)
        {
            await leaderboard.AddEntityAsync(LeaderboardKey, entity.Key);

            await leaderboard.UpdateEntityScoreAsync(LeaderboardKey, entity.Key, entity.Score);
        }

        var result = await leaderboard.GetEntityRankAsync(
            LeaderboardKey, entityKey, RankingType.DenseRank);

        result.Should().NotBeNull();
        result!.Value.Should().Be(expectedRank);
    }
    
    [Fact]
    public async Task GetEntityRankAsync_WithValidDataDenseRank_MultipleUpdates_ReturnsRank()
    {
        var leaderboard = _leaderboardFixture.Instance;
        const string entityKey = "player5";
        const int expectedRank = 4;

        var entities = new[]
        {
            new Player { Key = "player1", Score = 250 },
            new Player { Key = "player2", Score = 200 },
            new Player { Key = "player3", Score = 100 },
            new Player { Key = "player4", Score = 100 },
            new Player { Key = "player5", Score = 50 }
        };

        // randomize array
        _random.Shuffle(entities);

        foreach (var entity in entities)
        {
            await leaderboard.AddEntityAsync(LeaderboardKey, entity.Key);

            await leaderboard.UpdateEntityScoreAsync(LeaderboardKey, entity.Key, entity.Score);
        }
        
        //additional score update
        await leaderboard.UpdateEntityScoreAsync(LeaderboardKey, entityKey, 40);

        var result = await leaderboard.GetEntityRankAsync(
            LeaderboardKey, entityKey, RankingType.DenseRank);

        result.Should().NotBeNull();
        result!.Value.Should().Be(expectedRank);
    }
    
    [Fact]
    public async Task GetEntityRankAsync_WithValidDataStandardCompetition_ReturnsRank()
    {
        var leaderboard = _leaderboardFixture.Instance;
        const string entityKey = "player4";
        const int expectedRank = 3;

        var entities = new[]
        {
            new Player { Key = "player1", Score = 250 },
            new Player { Key = "player2", Score = 200 },
            new Player { Key = "player3", Score = 100 },
            new Player { Key = "player4", Score = 100 },
            new Player { Key = "player5", Score = 50 },
        };

        // randomize array
        _random.Shuffle(entities);

        foreach (var entity in entities)
        {
            await leaderboard.AddEntityAsync(LeaderboardKey, entity.Key);

            await leaderboard.UpdateEntityScoreAsync(LeaderboardKey, entity.Key, entity.Score);
        }

        var result = await leaderboard.GetEntityRankAsync(
            LeaderboardKey, entityKey, RankingType.StandardCompetition);

        result.Should().NotBeNull();
        result.Value.Should().Be(expectedRank);
    }
    
    [Fact]
    public async Task GetEntityRankAsync_WithValidDataModifiedCompetition_ReturnsRank()
    {
        var leaderboard = _leaderboardFixture.Instance;
        const string entityKey = "player4";
        const int expectedRank = 4;

        var entities = new[]
        {
            new Player { Key = "player1", Score = 250 },
            new Player { Key = "player2", Score = 200 },
            new Player { Key = "player3", Score = 100 },
            new Player { Key = "player4", Score = 100 },
            new Player { Key = "player5", Score = 50 },
        };

        // randomize array
        _random.Shuffle(entities);

        foreach (var entity in entities)
        {
            await leaderboard.AddEntityAsync(LeaderboardKey, entity.Key);

            await leaderboard.UpdateEntityScoreAsync(LeaderboardKey, entity.Key, entity.Score);
        }

        var result = await leaderboard.GetEntityRankAsync(
            LeaderboardKey, entityKey, RankingType.ModifiedCompetition);

        result.Should().NotBeNull();
        result.Value.Should().Be(expectedRank);
    }
    
    [Fact]
    public async Task UpdateEntityScoreAsync_WithValidData_ReturnsUpdatedScore()
    {
        var leaderboard = _leaderboardFixture.Instance;
        const string entityKey = "John";
        const double newScore = 150;

        var entities = new[]
        {
            new Player { Key = "Mike", Score = 200 },
            new Player { Key = "Alex", Score = 100 },
            new Player { Key = "John", Score = 100 },
            new Player { Key = "Sam", Score = 50 },
        };

        // randomize array
        _random.Shuffle(entities);
        
        foreach (var entity in entities)
        {
            await leaderboard.AddEntityAsync(LeaderboardKey, entity.Key);

            await leaderboard.UpdateEntityScoreAsync(LeaderboardKey, entity.Key, entity.Score);
        }
        
        await leaderboard.UpdateEntityScoreAsync(LeaderboardKey, entityKey, newScore);

        var result = await leaderboard.GetEntityScoreAsync(LeaderboardKey, entityKey);

        result.Should().NotBeNull();
        result.Value.Should().Be(newScore);
    }

    [Fact]
    public async Task UpdateEntityMedataAsync_WithValidData_ReturnsUpdatedMetadata()
    {
        var leaderboard = _leaderboardFixture.Instance;
        var entityKey = Guid.NewGuid().ToString();
        var metadata = new PlayerData()
        {
            EntryDate = DateTime.UtcNow,
            FirstName = "John",
            LastName = "Doe",
            Username = "nobody"
        };
        
        await leaderboard.AddEntityAsync(LeaderboardKey, entityKey);

        var metadataBeforeUpdate = await leaderboard.GetEntityMetadataAsync(LeaderboardKey, entityKey);

        metadataBeforeUpdate.Should().Be(default);
        
        await leaderboard.UpdateEntityMetadataAsync(LeaderboardKey, entityKey, JsonSerializer.Serialize(metadata));
        
        var metadataAfterUpdate = await leaderboard.GetEntityMetadataAsync(LeaderboardKey, entityKey);
        
        var deserializedMetadata = JsonSerializer.Deserialize<PlayerData>(metadataAfterUpdate);
        
        deserializedMetadata.FirstName.Should().Be(metadata.FirstName);
        deserializedMetadata.LastName.Should().Be(metadata.LastName);
        deserializedMetadata.Username.Should().Be(metadata.Username);
        deserializedMetadata.EntryDate.Should().Be(metadata.EntryDate);
    }
    
    [Fact]
    public async Task GetEntityAndNeighboursAsync_WithValidDataDefaultRanking_CorrectPageSize_ReturnsLeaderboard()
        => await TestCorrectPageSizeForGetEntityAndNeighboursAsync(RankingType.Default);

    [Fact]
    public async Task
        GetEntityAndNeighboursAsync_WithValidDataModifiedCompetitionRanking_CorrectPageSize_ReturnsLeaderboard()
        => await TestCorrectPageSizeForGetEntityAndNeighboursAsync(RankingType.ModifiedCompetition);

    [Fact]
    public async Task
        GetEntityAndNeighboursAsync_WithValidDataStandardCompetitionRanking_CorrectPageSize_ReturnsLeaderboard()
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
            .Select(i => new Player { Score = i, Key = $"player{i}" })
            .ToArray();

        // get a player to search for
        var playerIndexToSearchFor = random.Next(offset + 1, playersCount - offset);
        var playerKey = entities[playerIndexToSearchFor].Key;

        foreach (var entity in entities)
        {
            await leaderboard.AddEntityAsync(LeaderboardKey, entity.Key);

            await leaderboard.UpdateEntityScoreAsync(LeaderboardKey, entity.Key, entity.Score);
        }

        var result = await leaderboard
            .GetEntityAndNeighboursAsync(LeaderboardKey, playerKey, offset, rankingType);

        result.Should().NotBeNull();
        result.Should().HaveCount((offset * 2) + 1); // +1 for the player itself

        result.Should().OnlyContain(r => entities.Any(e => e.Key == r.Key));
        result.Should().OnlyContain(r => r.Score > 0);
    }

    public void Dispose() => _leaderboardFixture?.DeleteLeaderboardAsync();
}