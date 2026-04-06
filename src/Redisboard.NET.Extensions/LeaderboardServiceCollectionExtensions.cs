using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Redisboard.NET.Interfaces;
using Redisboard.NET.Serialization;
using StackExchange.Redis;

namespace Redisboard.NET.Extensions;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> to register leaderboard services.
/// </summary>
public static class LeaderboardServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ILeaderboard{TEntity}"/> → <see cref="Leaderboard{TEntity}"/> in the service collection
    /// using the default <see cref="SystemTextJsonLeaderboardSerializer"/>.
    /// </summary>
    /// <typeparam name="TEntity">
    /// The domain type stored in the leaderboard.
    /// Must implement <see cref="ILeaderboardEntity"/>, expose a parameterless constructor,
    /// and carry exactly one <see cref="Redisboard.NET.Attributes.LeaderboardKeyAttribute"/> and one
    /// <see cref="Redisboard.NET.Attributes.LeaderboardScoreAttribute"/> on its properties.
    /// </typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="optionsAction">
    /// An action to configure the Redis <see cref="ConfigurationOptions"/>.
    /// Not required if <see cref="IConnectionMultiplexer"/> is already registered in the container.
    /// </param>
    /// <returns>The updated service collection.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="optionsAction"/> is <c>null</c> and <see cref="IConnectionMultiplexer"/>
    /// is not already registered.
    /// </exception>
    public static IServiceCollection AddLeaderboard<TEntity>(
        this IServiceCollection services,
        Action<ConfigurationOptions> optionsAction = default)
        where TEntity : ILeaderboardEntity, new()
    {
        services.AddScoped<ILeaderboard<TEntity>, Leaderboard<TEntity>>();

        return RegisterRedis(services, optionsAction);
    }

    private static IServiceCollection RegisterRedis(
        IServiceCollection services,
        Action<ConfigurationOptions> optionsAction)
    {
        if (services.Any(s => s.ServiceType == typeof(IConnectionMultiplexer)))
            return services;

        if (optionsAction == default)
            throw new ArgumentNullException(
                nameof(optionsAction),
                "An options delegate must be provided if IConnectionMultiplexer is not already registered.");

        var redisOptions = new ConfigurationOptions();
        optionsAction.Invoke(redisOptions);

        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(redisOptions));

        return services;
    }
}
