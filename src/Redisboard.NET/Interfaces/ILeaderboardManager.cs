using Redisboard.NET.Enumerations;

namespace Redisboard.NET.Interfaces;

/// <summary>
/// Interface for managing leaderboards in Redis.
/// </summary>
/// <typeparam name="TEntity">The type of leaderboard entities.</typeparam>
public interface ILeaderboardManager<TEntity>
    where TEntity : ILeaderboardEntity
{
    /// <summary>
    /// Adds all the specified entities with the specified scores to the leaderboard at key.
    /// If a specified entity is already a member of the leaderboard, only the score is updated and the entity reinserted at the right position to ensure the correct ordering.
    /// </summary>
    /// <param name="leaderboardId">Unique identifier for the leaderboard</param>
    /// <param name="entities">The entity to add to the leaderboard.</param>
    /// <param name="fireAndForget">Utilizes a fire-and-forget approach to saving data in Redis. It's has an improved performance over waiting for the response from the server, however keep in mind that you won't notice any server errors that get reported.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task AddToLeaderboardAsync(
        object leaderboardId,
        TEntity[] entities,
        bool fireAndForget = default,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves a leaderboard entity by its unique identifier, including its neighbors.
    /// </summary>
    /// <param name="leaderboardId">Unique identifier for the leaderboard</param>
    /// <param name="id">The unique identifier of the leaderboard entity.</param>
    /// <param name="offset">The number of neighbours above and below the current entit</param>
    /// <param name="rankingType">The ranking type to use for ordering the leaderboard.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An array of leaderboard entities with the requested member in the middle.</returns>
    Task<TEntity[]> GetEntityAndNeighboursByIdAsync(
        object leaderboardId,
        string id,
        int offset = 10,
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

    /// <summary>
    /// Deletes the leaderboard by provided Id. If the leaderboard does not exist, nothing happens.
    /// </summary>
    /// <param name="leaderboardId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task DeleteLeaderboardAsync(
        object leaderboardId,
        CancellationToken cancellationToken = default);
}