using Redisboard.NET.Interfaces;

namespace Redisboard.NET.Models;

/// <summary>Represents an entity page and total membership from one leaderboard snapshot.</summary>
/// <remarks>The total counts ranked members, including legacy entries without metadata. Physical positions select rows regardless of tie ranking.</remarks>
/// <typeparam name="TEntity">The leaderboard entity type.</typeparam>
public sealed record LeaderboardPage<TEntity>
    where TEntity : ILeaderboardEntity
{
    /// <summary>Initializes a page from one leaderboard snapshot.</summary>
    /// <param name="entities">The selected entities in leaderboard order.</param>
    /// <param name="totalCount">The total number of ranked members at the same instant.</param>
    /// <remarks>Retains the supplied array without copying. Returned entities are detached snapshots.</remarks>
    public LeaderboardPage(TEntity[] entities, long totalCount)
    {
        this.Entities = entities;
        this.TotalCount = totalCount;
    }

    /// <summary>Gets selected entities in leaderboard order.</summary>
    public TEntity[] Entities { get; }

    /// <summary>Gets the number of ranked members in the snapshot.</summary>
    public long TotalCount { get; }
}
