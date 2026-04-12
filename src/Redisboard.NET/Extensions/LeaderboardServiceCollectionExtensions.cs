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
    /// <param name="optionsAction">Configures Redis connection options when neither <see cref="IConnectionMultiplexer"/> nor <see cref="IDatabase"/> is already registered. When <see langword="null"/>, an existing Redis registration is required.</param>
    /// <param name="databaseIndex">Zero-based Redis database index resolved for <see cref="Leaderboard{TEntity}"/> instances when <see cref="IConnectionMultiplexer"/> is used. This value is ignored when <see cref="IDatabase"/> is resolved directly from dependency injection.</param>
    /// <param name="serializer">Serializer used for entity metadata. When <see langword="null"/>, <see cref="MemoryPackLeaderboardSerializer"/> is registered.</param>
    /// <returns>Same <paramref name="services"/> instance so calls can be chained.</returns>
    /// <remarks>
    /// <para>
    /// This method registers serializer and leaderboard services as singletons. Reuse a shared
    /// <see cref="IConnectionMultiplexer"/> whenever possible because StackExchange.Redis connections are
    /// designed for long-lived application scope.
    /// </para>
    /// <para>
    /// This method prefers an existing <see cref="IConnectionMultiplexer"/> registration because it can resolve
    /// the target database on demand and keeps <paramref name="databaseIndex"/> meaningful.
    /// </para>
    /// <para>
    /// When no <see cref="IConnectionMultiplexer"/> is registered, this method falls back to an existing
    /// <see cref="IDatabase"/> registration. In that case, <paramref name="databaseIndex"/> is ignored because
    /// database selection has already happened before <see cref="Leaderboard{TEntity}"/> is constructed.
    /// </para>
    /// <para>
    /// If neither <see cref="IConnectionMultiplexer"/> nor <see cref="IDatabase"/> is already registered, this
    /// method builds a new multiplexer from <paramref name="optionsAction"/>.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="optionsAction"/> is <see langword="null"/> and neither <see cref="IConnectionMultiplexer"/> nor <see cref="IDatabase"/> is already registered.</exception>
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
            var leaderboardSerializer = sp.GetRequiredService<ILeaderboardSerializer>();

            var multiplexer = sp.GetService<IConnectionMultiplexer>();
            if (multiplexer is not null)
                return new Leaderboard<TEntity>(multiplexer, leaderboardSerializer, databaseIndex);

            var database = sp.GetService<IDatabase>();
            if (database is not null)
                return new Leaderboard<TEntity>(database, leaderboardSerializer);

            throw new InvalidOperationException(
                $"No Redis service registration found for {typeof(TEntity).Name}. Register IConnectionMultiplexer, IDatabase, or provide an options delegate.");
        });

        return RegisterRedis(services, optionsAction);
    }

    private static IServiceCollection RegisterRedis(
        IServiceCollection services,
        Action<ConfigurationOptions> optionsAction)
    {
        if (services.Any(s => s.ServiceType == typeof(IConnectionMultiplexer))
            || services.Any(s => s.ServiceType == typeof(IDatabase)))
            return services;

        if (optionsAction is null)
            throw new ArgumentNullException(
                nameof(optionsAction),
                "An options delegate must be provided if neither IConnectionMultiplexer nor IDatabase is already registered.");

        var redisOptions = new ConfigurationOptions();
        optionsAction.Invoke(redisOptions);

        services.AddSingleton<IConnectionMultiplexer>(
            _ => ConnectionMultiplexer.Connect(redisOptions));

        return services;
    }
}
