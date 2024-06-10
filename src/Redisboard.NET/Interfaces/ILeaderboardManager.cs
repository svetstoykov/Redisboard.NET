using Redisboard.NET.Enumerations;

namespace Redisboard.NET.Interfaces;

/// <summary>
/// Interface for managing leaderboards in Redis.
/// </summary>
/// <typeparam name="TEntity">The type of leaderboard entities.</typeparam>
/// <typeparam name="TId">The type of the unique identifier for leaderboard entities.</typeparam>
public interface ILeaderboardManager<TEntity>
    where TEntity : ILeaderboardEntity
{
    /// <summary>
    /// Adds all the specified entities with the specified scores to the leaderboard at key.
    /// If a specified entity is already a member of the leaderboard, only the score is updated and the entity reinserted at the right position to ensure the correct ordering.
    /// </summary>
    /// <param name="leaderboardId">Unique identifier for the leaderboard</param>
    /// <param name="entities">The entity to add to the leaderboard.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task AddToLeaderboardAsync(
        object leaderboardId,
        TEntity[] entities,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Adds all the specified entities with the specified scores to the leaderboard at key.
    /// If a specified entity is already a member of the leaderboard, only the score is updated and the entity reinserted at the right position to ensure the correct ordering.
    /// </summary>
    /// <param name="leaderboardId">Unique identifier for the leaderboard</param>
    /// <param name="entities">The entity to add to the leaderboard.</param>
    /// <remarks>This method utilizes a fire-and-forget approach to saving data in Redis. It's has an improved performance over <see cref="AddToLeaderboardAsync"/>, however keep in mind that you won't notice any server errors that get reported.</remarks>
    void AddToLeaderboard(
        object leaderboardId,
        TEntity[] entities);

    /// <summary>
    /// Retrieves a leaderboard entity by its unique identifier asynchronously.
    /// </summary>
    /// <param name="leaderboardId">Unique identifier for the leaderboard</param>
    /// <param name="id">The unique identifier of the leaderboard entity.</param>
    /// <param name="rankingType">The ranking type to use for ordering the leaderboard.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The leaderboard entity with the specified identifier.</returns>
    Task<TEntity> GetLeaderboardEntityByIdAsync(
        object leaderboardId,
        string id,
        RankingType rankingType = default,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a paginated view of the leaderboard asynchronously.
    /// </summary>
    /// <param name="leaderboardId">Unique identifier for the leaderboard</param>
    /// <param name="pageIndex">The index of the page to retrieve (0-based).</param>
    /// <param name="pageSize">The size of each page.</param>
    /// <param name="rankingType">The ranking type to use for ordering the leaderboard.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A paginated view of the leaderboard.</returns>
    Task<TEntity[]> GetPaginatedLeaderboardAsync(
        object leaderboardId,
        int pageIndex,
        int pageSize,
        RankingType rankingType = default,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a subset of the leaderboard based on score range asynchronously.
    /// </summary>
    /// <param name="leaderboardId">Unique identifier for the leaderboard</param>
    /// <param name="minScoreValue">The minimum score value (inclusive).</param>
    /// <param name="maxScoreValue">The maximum score value (exclusive).</param>
    /// <param name="rankingType">The ranking type to use for ordering the leaderboard.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A subset of the leaderboard based on score range.</returns>
    Task<TEntity[]> GetLeaderboardByScoreRangeAsync(
        object leaderboardId,
        double minScoreValue,
        double maxScoreValue,
        RankingType rankingType = default,
        CancellationToken cancellationToken = default);
}