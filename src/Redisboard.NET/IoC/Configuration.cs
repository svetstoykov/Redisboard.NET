using Microsoft.Extensions.DependencyInjection;
using Redisboard.NET.Interfaces;
using Redisboard.NET.Services;
using Redisboard.NET.Settings;
using StackExchange.Redis;

namespace Redisboard.NET.IoC;

public static class Configuration
{
    /// <summary>
    /// Registers the <see cref="ILeaderboardManager{TEntity}"/> service for dependency injection, which provides CRUD operations for leaderboard management.
    /// </summary>
    /// <typeparam name="TEntity">The type of leaderboard entities.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="settings">The Redis settings for configuring the connection, optional if <see cref="IConnectionMultiplexer"/> is already registered.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>   
    public static IServiceCollection AddLeaderboardManager<TEntity>(this IServiceCollection services, RedisSettings settings = default)
        where TEntity : ILeaderboardEntity
    {
        services.AddScoped<ILeaderboardManager<TEntity>, LeaderboardManager<TEntity>>();

        if (services.Any(s => s.ServiceType == typeof(IConnectionMultiplexer)))
        {
            return services;
        }

        if (settings == default)
        {
            throw new ArgumentNullException();
        }
        
        var config = settings.Configuration; 
        
        config.EndPoints.Add(settings.ConnectionString);
        
        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(config));

        return services;
    }
}