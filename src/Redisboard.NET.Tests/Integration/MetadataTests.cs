using FluentAssertions;
using Redisboard.NET.Common.Models;

namespace Redisboard.NET.Tests.Integration;

/// <summary>
/// Tests that player domain properties (Username, FirstName, LastName, EntryDate) are correctly
/// persisted and retrieved by the generic leaderboard. Also covers <see cref="Leaderboard{T}.GetSizeAsync"/>
/// and metadata updates via <see cref="Leaderboard{T}.UpdateEntityMetadataAsync"/>.
/// </summary>
public class MetadataTests : LeaderboardTestBase
{
    public MetadataTests(LeaderboardFixture fixture) : base(fixture) { }

    [Fact]
    public async Task AddEntityAsync_WithDomainProperties_PropertiesAreRetrievable()
    {
        var player = new Player
        {
            Id = Guid.NewGuid().ToString(),
            Score = 500,
            Username = "insert_user",
            FirstName = "Insert",
            LastName = "Test",
            EntryDate = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc)
        };

        await Leaderboard.AddEntityAsync(Key, player);

        var results = await Leaderboard.GetEntitiesByRankRangeAsync(Key, 1, 1);
        var stored = results.Single();

        stored.Username.Should().Be(player.Username);
        stored.FirstName.Should().Be(player.FirstName);
        stored.LastName.Should().Be(player.LastName);
        stored.EntryDate.Should().BeCloseTo(player.EntryDate, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task UpdateEntityMetadataAsync_ReflectsNewDomainProperties()
    {
        var player = new Player
        {
            Id = Guid.NewGuid().ToString(),
            Score = 100,
            Username = "original",
            FirstName = "John",
            LastName = "Doe",
            EntryDate = DateTime.UtcNow
        };

        await Leaderboard.AddEntityAsync(Key, player);

        player.Username = "updated";
        player.FirstName = "Jane";

        await Leaderboard.UpdateEntityMetadataAsync(Key, player);

        var results = await Leaderboard.GetEntitiesByRankRangeAsync(Key, 1, 1);
        var stored = results.Single();

        stored.Username.Should().Be("updated");
        stored.FirstName.Should().Be("Jane");
    }

    [Fact]
    public async Task GetSizeAsync_WithEntities_ReturnsCorrectCount()
    {
        const int count = 100;

        var sem = new SemaphoreSlim(50);
        var tasks = Enumerable.Range(0, count).Select(async i =>
        {
            await sem.WaitAsync();
            try
            {
                var p = new Player { Id = $"sz_{i}", Score = i, Username = $"user_{i}" };
                await Leaderboard.AddEntityAsync(Key, p);
            }
            finally { sem.Release(); }
        });
        await Task.WhenAll(tasks);

        var size = await Leaderboard.GetSizeAsync(Key);

        size.Should().Be(count);
    }

    [Fact]
    public async Task GetSizeAsync_AfterDeleteEntity_DecreasesCount()
    {
        const int count = 10;

        for (var i = 0; i < count; i++)
        {
            var p = new Player { Id = $"szdel_{i}", Score = i, Username = $"u{i}" };
            await Leaderboard.AddEntityAsync(Key, p);
        }

        await Leaderboard.DeleteEntityAsync(Key, "szdel_0");

        var size = await Leaderboard.GetSizeAsync(Key);

        size.Should().Be(count - 1);
    }

    [Fact]
    public async Task AddEntitiesAsync_BatchAdd_PersistsScoresAndMetadataForAllEntities()
    {
        var players = new[]
        {
            new Player { Id = "batch_meta_1", Score = 350, Username = "u1", FirstName = "Ada", LastName = "Lovelace", EntryDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Player { Id = "batch_meta_2", Score = 420, Username = "u2", FirstName = "Grace", LastName = "Hopper", EntryDate = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Player { Id = "batch_meta_3", Score = 120, Username = "u3", FirstName = "Alan", LastName = "Turing", EntryDate = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc) }
        };

        await Leaderboard.AddEntitiesAsync(Key, players);

        var board = await Leaderboard.GetEntitiesByRankRangeAsync(Key, 1, players.Length);
        var byId = board.ToDictionary(p => p.Id);

        foreach (var expected in players)
        {
            byId.Should().ContainKey(expected.Id);

            var stored = byId[expected.Id];
            stored.Score.Should().Be(expected.Score);
            stored.Username.Should().Be(expected.Username);
            stored.FirstName.Should().Be(expected.FirstName);
            stored.LastName.Should().Be(expected.LastName);
            stored.EntryDate.Should().Be(expected.EntryDate);

            var score = await Leaderboard.GetEntityScoreAsync(Key, expected.Id);
            score.Should().Be(expected.Score);
        }
    }
}
