using Redisboard.NET.Interfaces;

namespace Redisboard.NET.Helpers;

internal static class Guard
{
    public static void AgainstInvalidLeaderboardKey(object leaderboardKey)
    {
        if (leaderboardKey == default)
        {
            throw new ArgumentNullException(nameof(leaderboardKey), "Invalid leaderboard key!");
        }
    }

    public static void AgainstInvalidLeaderboardEntities<TEntity>(TEntity[] entities)
        where TEntity : ILeaderboardEntity
    {
        if (entities == null)
        {
            throw new ArgumentNullException(nameof(entities), "The leaderboard entities array cannot be null.");
        }
    }

    public static void AgainstInvalidEntityKey(string entityKey)
    {
        if (string.IsNullOrEmpty(entityKey))
        {
            throw new ArgumentNullException(nameof(entityKey), "Invalid entity key!");
        }
    }
    
    public static void AgainstInvalidOffset(int offset)
    {
        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be negative!");
        }
    }
    
    public static void AgainstInvalidScoreRangeLimit(double limit)
    {
        if (limit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Score range limit cannot be negative!");
        }
    }
}