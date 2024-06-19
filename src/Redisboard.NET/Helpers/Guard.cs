using Redisboard.NET.Interfaces;

namespace Redisboard.NET.Helpers;

internal static class Guard
{
    public static void AgainstInvalidLeaderboardKey(object leaderboardId)
    {
        if (leaderboardId == default)
        {
            throw new ArgumentNullException(nameof(leaderboardId), "Invalid leaderboard identifier");
        }
    }

    public static void AgainstInvalidLeaderboardEntities<TEntity>(TEntity[] entities)
        where TEntity : ILeaderboardEntity
    {
        if (entities == null)
        {
            throw new ArgumentNullException(nameof(entities), "The leaderboards entities array cannot be null.");
        }
    }

    public static void AgainstInvalidEntityId(string entityId)
    {
        if (string.IsNullOrEmpty(entityId))
        {
            
        }
    }
}