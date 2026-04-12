using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Redisboard.NET.Interfaces;
using Redisboard.NET.Serialization;
using StackExchange.Redis;

namespace Redisboard.NET.Extensions;

/// <summary>
/// Provides dependency injection registration helpers for leaderboard services.
/// </summary>
/// <remarks>
/// These extensions register <see cref="ILeaderboard{TEntity}"/> together with the Redis connection and
/// serializer dependencies required by <see cref="Leaderboard{TEntity}"/>.
/// </remarks>
public static class LeaderboardServiceCollectionExtensions
{
    /// <summary>
    /// Registers leaderboard services for an entity type.
    /// </summary>
    /// <typeparam name="TEntity">
    /// Entity type stored in leaderboard. It must implement <see cref="ILeaderboardEntity"/>, expose a
    /// parameterless constructor, and declare exactly one key property and one score property by using
    /// <see cref="Redisboard.NET.Attributes.LeaderboardKeyAttribute"/> and
    /// <see cref="Redisboard.NET.Attributes.LeaderboardScoreAttribute"/>.
    /// </typeparam>
    /// <param name="services">Service collection that receives leaderboard registrations.</param>
    /// <param name="optionsAction">Configures Redis connection options when <see cref="IConnectionMultiplexer"/> is not already registered. When <see langword="null"/>, an existing multiplexer registration is required.</param>
    /// <param name="databaseIndex">Zero-based Redis database index resolved for <see cref="Leaderboard{TEntity}"/> instances.</param>
    /// <param name="serializer">Serializer used for entity metadata. When <see langword="null"/>, <see cref="MemoryPackLeaderboardSerializer"/> is registered.</param>
    /// <returns>Same <paramref name="services"/> instance so calls can be chained.</returns>
    /// <remarks>
    /// <para>
    /// This method registers serializer and leaderboard services as singletons. Reuse a shared
    /// <see cref="IConnectionMultiplexer"/> whenever possible because StackExchange.Redis connections are
    /// designed for long-lived application scope.
    /// </para>
    /// <para>
    /// If no <see cref="IConnectionMultiplexer"/> is already registered, this method builds one from
    /// <paramref name="optionsAction"/>.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="optionsAction"/> is <see langword="null"/> and <see cref="IConnectionMultiplexer"/> is not already registered.</exception>
    public static IServiceCollection AddLeaderboard<TEntity>(
        this IServiceCollection services,
        Action<ConfigurationOptions> optionsAction = default,
        int databaseIndex = 0,
        ILeaderboardSerializer serializer = default)
        where TEntity : ILeaderboardEntity, new()
    {
        serializer ??= new MemoryPackLeaderboardSerializer();

        services.AddSingleton(serializer);

        services.AddSingleton<ILeaderboard<TEntity>>(sp =>
        {
            var multiplexer = sp.GetRequiredService<IConnectionMultiplexer>();
            var leaderboardSerializer = sp.GetRequiredService<ILeaderboardSerializer>();
            return new Leaderboard<TEntity>(multiplexer, leaderboardSerializer, databaseIndex);
        });

        return RegisterRedis(services, optionsAction);
    }

    private static IServiceCollection RegisterRedis(
        IServiceCollection services,
        Action<ConfigurationOptions> optionsAction)
    {
        if (services.Any(s => s.ServiceType == typeof(IConnectionMultiplexer)))
            return services;

        if (optionsAction is null)
            throw new ArgumentNullException(
                nameof(optionsAction),
                "An options delegate must be provided if IConnectionMultiplexer is not already registered.");

        var redisOptions = new ConfigurationOptions();
        optionsAction.Invoke(redisOptions);

        services.AddSingleton<IConnectionMultiplexer>(
            sp => ConnectionMultiplexer.Connect(redisOptions));

        return services;
    }
}
