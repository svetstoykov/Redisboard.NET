using Microsoft.Extensions.DependencyInjection;
using Moq;
using Redisboard.NET.Attributes;
using Redisboard.NET.Common.Models;
using Redisboard.NET.Exceptions;
using Redisboard.NET.Extensions;
using Redisboard.NET.Interfaces;
using Redisboard.NET.Serialization;
using StackExchange.Redis;

namespace Redisboard.NET.Tests.UnitTests;

public class DependencyInjectionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AddLeaderboard_WithExistingRedisAndOptions_RejectsIgnoredOptions(bool multiplexer)
    {
        var services = new ServiceCollection();
        if (multiplexer)
            services.AddSingleton(Mock.Of<IConnectionMultiplexer>());
        else
            services.AddSingleton(Mock.Of<IDatabase>());
        var originalCount = services.Count;

        var exception = Assert.Throws<ArgumentException>(() => services.AddLeaderboard<Player>(_ => { }));

        Assert.Equal("optionsAction", exception.ParamName);
        Assert.Equal(originalCount, services.Count);
    }

    [Fact]
    public void AddLeaderboard_WithSecondOptionsDelegate_RejectsIgnoredOptions()
    {
        var services = new ServiceCollection();
        services.AddLeaderboard<Player>(options => options.EndPoints.Add("localhost:6379"));
        var originalCount = services.Count;

        Assert.Throws<ArgumentException>(() => services.AddLeaderboard<Player>(options => options.EndPoints.Add("localhost:6380")));

        Assert.Equal(originalCount, services.Count);
    }

    [Fact]
    public void AddLeaderboard_WithInvalidEntity_ThrowsConfigurationExceptionBeforeMutation()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IDatabase>());
        var originalCount = services.Count;

        var exception = Assert.Throws<LeaderboardConfigurationException>(() => services.AddLeaderboard<MissingKeyEntity>());

        Assert.Contains("LeaderboardKeyAttribute", exception.Message);
        Assert.Equal(originalCount, services.Count);
    }

    [Fact]
    public void Constructor_WithInvalidEntity_ThrowsConfigurationExceptionDirectly()
    {
        var database = Mock.Of<IDatabase>();

        var exception = Assert.Throws<LeaderboardConfigurationException>(() =>
            new Leaderboard<MissingScoreEntity>(database, new MemoryPackLeaderboardSerializer()));

        Assert.Contains("LeaderboardScoreAttribute", exception.Message);
    }

    [Fact]
    public void AddLeaderboard_WithoutRedisOrOptions_LeavesCollectionUnchanged()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddLeaderboard<Player>());

        Assert.Empty(services);
    }

    public class MissingKeyEntity : ILeaderboardEntity
    {
        [LeaderboardScore]
        public double Score { get; set; }
        public long Rank { get; set; }
    }

    public class MissingScoreEntity : ILeaderboardEntity
    {
        [LeaderboardKey]
        public string Id { get; set; } = "";
        public long Rank { get; set; }
    }
}
