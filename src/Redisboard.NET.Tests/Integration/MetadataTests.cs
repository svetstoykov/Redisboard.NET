using System.Text.Json;
using FluentAssertions;
using Redisboard.NET.Common.Models;

namespace Redisboard.NET.Tests.Integration;

/// <summary>
/// Tests for metadata operations: <see cref="Leaderboard.AddEntityAsync"/> with metadata,
/// <see cref="Leaderboard.GetEntityMetadataAsync"/>, <see cref="Leaderboard.UpdateEntityMetadataAsync"/>,
/// and <see cref="Leaderboard.GetSizeAsync"/> (which counts entities in the metadata hash-set).
/// </summary>
public class MetadataTests : LeaderboardTestBase
{
    public MetadataTests(LeaderboardFixture fixture) : base(fixture) { }

    [Fact]
    public async Task AddEntityAsync_WithMetadata_MetadataIsImmediatelyRetrievable()
    {
        var entityKey = Guid.NewGuid().ToString();
        var metadata = new PlayerData
        {
            Username = "insert_user",
            FirstName = "Insert",
            LastName = "Test",
            EntryDate = DateTime.UtcNow
        };
        var serialized = JsonSerializer.Serialize(metadata);

        await Leaderboard.AddEntityAsync(Key, entityKey, serialized);

        var stored = await Leaderboard.GetEntityMetadataAsync(Key, entityKey);

        stored.Should().NotBe(default);
        var deserialized = JsonSerializer.Deserialize<PlayerData>(stored!);
        deserialized!.Username.Should().Be(metadata.Username);
        deserialized.FirstName.Should().Be(metadata.FirstName);
        deserialized.LastName.Should().Be(metadata.LastName);
    }

    [Fact]
    public async Task AddEntityAsync_WithoutMetadata_MetadataIsDefault()
    {
        var entityKey = Guid.NewGuid().ToString();

        await Leaderboard.AddEntityAsync(Key, entityKey);

        var stored = await Leaderboard.GetEntityMetadataAsync(Key, entityKey);

        stored.Should().Be(default);
    }

    [Fact]
    public async Task UpdateEntityMetadataAsync_ReturnsUpdatedMetadata()
    {
        var entityKey = Guid.NewGuid().ToString();
        var metadata = new PlayerData
        {
            EntryDate = DateTime.UtcNow,
            FirstName = "John",
            LastName = "Doe",
            Username = "nobody"
        };

        await Leaderboard.AddEntityAsync(Key, entityKey);

        var metadataBeforeUpdate = await Leaderboard.GetEntityMetadataAsync(Key, entityKey);
        metadataBeforeUpdate.Should().Be(default);

        await Leaderboard.UpdateEntityMetadataAsync(Key, entityKey, JsonSerializer.Serialize(metadata));

        var metadataAfterUpdate = await Leaderboard.GetEntityMetadataAsync(Key, entityKey);
        var deserialized = JsonSerializer.Deserialize<PlayerData>(metadataAfterUpdate);

        deserialized!.FirstName.Should().Be(metadata.FirstName);
        deserialized.LastName.Should().Be(metadata.LastName);
        deserialized.Username.Should().Be(metadata.Username);
        deserialized.EntryDate.Should().Be(metadata.EntryDate);
    }

    [Fact]
    public async Task GetSizeAsync_WithMetadataEntities_ReturnsCorrectCount()
    {
        const int count = 100;
        var metadata = JsonSerializer.Serialize(new PlayerData
        {
            Username = "test_user",
            FirstName = "Size",
            LastName = "Test",
            EntryDate = DateTime.UtcNow
        });

        var sem = new SemaphoreSlim(50);
        var tasks = Enumerable.Range(0, count).Select(async i =>
        {
            await sem.WaitAsync();
            try
            {
                var key = $"sz_{i}";
                await Leaderboard.AddEntityAsync(Key, key, metadata);
                await Leaderboard.UpdateEntityScoreAsync(Key, key, i);
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
        var metadata = JsonSerializer.Serialize(new PlayerData
        {
            Username = "sz_user", FirstName = "A", LastName = "B", EntryDate = DateTime.UtcNow
        });

        for (var i = 0; i < count; i++)
        {
            var key = $"szdel_{i}";
            await Leaderboard.AddEntityAsync(Key, key, metadata);
            await Leaderboard.UpdateEntityScoreAsync(Key, key, i);
        }

        await Leaderboard.DeleteEntityAsync(Key, "szdel_0");

        var size = await Leaderboard.GetSizeAsync(Key);

        size.Should().Be(count - 1);
    }
}
