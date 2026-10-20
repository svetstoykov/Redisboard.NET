using Microsoft.Extensions.DependencyInjection;
using Moq;
using Redisboard.NET.Common.Models;
using Redisboard.NET.Extensions;
using Redisboard.NET.Serialization;
using StackExchange.Redis;

namespace Redisboard.NET.Tests.UnitTests;

public class KeyConfigurationTests
{
    [Theory]
    [InlineData("prefix{tag}")]
    [InlineData("prefix{")]
    [InlineData("prefix}")]
    public void Constructor_WithClusterPrefixContainingBraces_RejectsAmbiguousHashTags(string prefix)
    {
        var database = Mock.Of<IDatabase>();
        var options = new LeaderboardOptions { KeyPrefix = prefix, UseClusterHashTags = true };

        Assert.Throws<ArgumentException>(() => new Leaderboard<Player>(database, new MemoryPackLeaderboardSerializer(), options));
    }

    [Fact]
    public void Constructor_WithNullPrefix_RejectsInvalidConfiguration()
    {
        var database = Mock.Of<IDatabase>();

        Assert.Throws<ArgumentNullException>(() => new Leaderboard<Player>(database,
            new MemoryPackLeaderboardSerializer(), new LeaderboardOptions { KeyPrefix = null! }));
    }

    [Fact]
    public void AddLeaderboard_WithNullConnectionDelegate_PreservesExistingOverloadResolution()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IDatabase>());

        Assert.Same(services, services.AddLeaderboard<Player>(null));
    }
}
