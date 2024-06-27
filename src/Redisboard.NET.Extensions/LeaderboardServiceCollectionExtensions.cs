using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Redisboard.NET.Interfaces;
using StackExchange.Redis;

namespace Redisboard.NET.Extensions;

/// <summary>
/// Extension methods for IServiceCollection to add leaderboard manager services.
/// </summary>
public static class LeaderboardServiceCollectionExtensions
{
    /// <summary>
    /// Register the leaderboard services in the service collection.
    /// </summary>
    /// <typeparam name="TEntity">The type of the leaderboard entity.</typeparam>
    /// <param name="services">The service collection to which the services are added.</param>
    /// <param name="optionsAction">An optional action to configure the Redis connection options, not required if <see cref="IConnectionMultiplexer"/> is already registered.</param>
    /// <returns>The updated service collection.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the optionsAction is null and IConnectionMultiplexer is not already registered.
    /// </exception>
    public static IServiceCollection AddLeaderboard<TEntity>(
        this IServiceCollection services, 
        Action<ConfigurationOptions> optionsAction = default)
        where TEntity : ILeaderboardEntity
    {
        services.AddScoped<ILeaderboard<TEntity>, Leaderboard<TEntity>>();

        if (services.Any(s => s.ServiceType == typeof(IConnectionMultiplexer)))
        {
            return services;
        }

        if (optionsAction == default)
        {
            throw new ArgumentNullException(
                nameof(optionsAction), "An options delegate must be provided if IConnectionMultiplexer is not already registered.");
        }

        var redisOptions = new ConfigurationOptions();

        optionsAction.Invoke(redisOptions);
        
        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(redisOptions));

        return services;
    }
}