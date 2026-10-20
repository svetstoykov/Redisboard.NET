using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Redisboard.NET.Interfaces;
using Redisboard.NET.Helpers;
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
    /// <param name="optionsAction">Configures Redis connection options when neither <see cref="IConnectionMultiplexer"/> nor <see cref="IDatabase"/> is already registered. Must be omitted when reusing an existing Redis registration.</param>
    /// <param name="databaseIndex">Zero-based Redis database index, or -1 to use the configured default, resolved for <see cref="Leaderboard{TEntity}"/> instances when <see cref="IConnectionMultiplexer"/> is used. This value is ignored when <see cref="IDatabase"/> is resolved directly from dependency injection.</param>
    /// <param name="serializer">Serializer used for entity metadata. When <see langword="null"/>, <see cref="MemoryPackLeaderboardSerializer"/> is used.</param>
    /// <returns>Same <paramref name="services"/> instance so calls can be chained.</returns>
    /// <remarks>
    /// <para>
    /// This method registers each leaderboard as a singleton and retains its own serializer instance. Reuse a shared
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
    /// <exception cref="ArgumentException">Thrown when <paramref name="optionsAction"/> is supplied alongside an existing Redis registration.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="optionsAction"/> is <see langword="null"/> and neither <see cref="IConnectionMultiplexer"/> nor <see cref="IDatabase"/> is already registered.</exception>
    /// <exception cref="Redisboard.NET.Exceptions.LeaderboardConfigurationException">Thrown at registration when the entity key or score configuration is invalid.</exception>
    public static IServiceCollection AddLeaderboard<TEntity>(
        this IServiceCollection services,
        Action<ConfigurationOptions> optionsAction = default,
        int databaseIndex = -1,
        ILeaderboardSerializer serializer = default)
        where TEntity : ILeaderboardEntity, new()
        => AddLeaderboard<TEntity>(services, optionsAction, new LeaderboardOptions(), databaseIndex, serializer);

    /// <summary>Registers leaderboard services with explicit key naming options.</summary>
    /// <typeparam name="TEntity">Entity type with configured key and score properties.</typeparam>
    /// <param name="services">Service collection receiving singleton leaderboard registration.</param>
    /// <param name="leaderboardOptions">Key naming settings copied at registration.</param>
    /// <param name="optionsAction">Configures a new Redis connection; pass null when Redis is already registered.</param>
    /// <param name="databaseIndex">Database index, or -1 for the configured default; ignored for an existing database.</param>
    /// <param name="serializer">Entity serializer; null selects MemoryPack.</param>
    /// <returns>The same collection for fluent chaining.</returns>
    /// <remarks>
    /// Retains existing Redis service ownership and selection rules. Each entity registration captures its own
    /// <see cref="LeaderboardOptions"/> and serializer without building an internal service provider.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when key settings are null, or no Redis service or connection configuration exists.</exception>
    /// <exception cref="ArgumentException">Thrown for braces in a cluster prefix or connection configuration alongside an existing Redis service.</exception>
    /// <exception cref="Redisboard.NET.Exceptions.LeaderboardConfigurationException">Thrown when entity configuration is invalid.</exception>
    public static IServiceCollection AddLeaderboard<TEntity>(this IServiceCollection services,
        Action<ConfigurationOptions> optionsAction,
        LeaderboardOptions leaderboardOptions,
        int databaseIndex = -1,
        ILeaderboardSerializer serializer = default)
        where TEntity : ILeaderboardEntity, new()
    {
        if (leaderboardOptions is null)
            throw new ArgumentNullException(nameof(leaderboardOptions));
        var snapshot = leaderboardOptions.Snapshot();
        EntityTypeAccessor<TEntity>.Validate();
        RegisterRedis(services, optionsAction);
        serializer ??= new MemoryPackLeaderboardSerializer();

        services.AddSingleton<ILeaderboard<TEntity>>(sp =>
        {
            var multiplexer = sp.GetService<IConnectionMultiplexer>();
            if (multiplexer is not null)
                return new Leaderboard<TEntity>(multiplexer, serializer, snapshot, databaseIndex);

            var database = sp.GetService<IDatabase>();
            if (database is not null)
                return new Leaderboard<TEntity>(database, serializer, snapshot);

            throw new InvalidOperationException(
                $"No Redis service registration found for {typeof(TEntity).Name}. Register IConnectionMultiplexer, IDatabase, or provide an options delegate.");
        });

        return services;
    }

    private static IServiceCollection RegisterRedis(
        IServiceCollection services,
        Action<ConfigurationOptions> optionsAction)
    {
        if (services.Any(s => s.ServiceType == typeof(IConnectionMultiplexer))
            || services.Any(s => s.ServiceType == typeof(IDatabase)))
        {
            if (optionsAction is not null)
                throw new ArgumentException(
                    "Redis is already registered. Omit the options delegate to reuse the existing connection or database.",
                    nameof(optionsAction));

            return services;
        }

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
