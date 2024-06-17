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
    Task AddEnitityToLeaderboardAsync(
        object leaderboardId,
        TEntity[] entities,
        bool fireAndForget = default,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves a leaderboard entity by its unique identifier, including its neighbors.
    /// </summary>
    /// <param name="leaderboardId">Unique identifier for the leaderboard</param>
    /// <param name="entityId">The unique identifier of the leaderboard entity.</param>
    /// <param name="offset">The number of neighbours to retrieve before and after the current entity</param>
    /// <param name="rankingType">The ranking type to use for ordering the leaderboard.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An array of leaderboard entities with the requested member in the middle.</returns>
    Task<TEntity[]> GetEntityAndNeighboursByIdAsync(
        object leaderboardId,
        string entityId,
        int offset = 10,
        RankingType rankingType = RankingType.Default,
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
        RankingType rankingType = RankingType.Default,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the data of a specific entity from the leaderboard asynchronously.
    /// </summary>
    /// <param name="leaderboardId">The identifier of the leaderboard.</param>
    /// <param name="entityId">The identifier of the entity whose data is to be retrieved.</param>
    /// <param name="cancellationToken">Optional. A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the entity data.</returns>
    Task<TEntity> GetEntityDataByIdAsync(
        object leaderboardId, 
        string entityId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the score of a specific entity from the leaderboard asynchronously.
    /// </summary>
    /// <param name="leaderboardId">The identifier of the leaderboard.</param>
    /// <param name="entityId">The identifier of the entity whose score is to be retrieved.</param>
    /// <param name="cancellationToken">Optional. A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the score of the entity.</returns>
    Task<double> GetEntityScoreByIdAsync(
        object leaderboardId, 
        string entityId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the rank of a specific entity in the leaderboard asynchronously.
    /// </summary>
    /// <param name="entityId">The identifier of the entity whose rank is to be retrieved.</param>
    /// <param name="cancellationToken">Optional. A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the rank of the entity.</returns>
    Task<long> GetEntityRankByIdAsync(string entityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a specific entity from the leaderboard.
    /// </summary>
    /// <param name="entityId">The identifier of the entity to be removed.</param>
    /// <param name="cancellationToken">Optional. A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task RemoveEntityByIdAsync(string entityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the size of the leaderboard.
    /// </summary>
    /// <param name="leaderboardId">The identifier of the leaderboard.</param>
    /// <param name="cancellationToken">Optional. A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the size of the leaderboard.</returns>
    Task<long> GetLeaderboardSizeAsync(object leaderboardId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all entries from the leaderboard.
    /// </summary>
    /// <param name="leaderboardId">The identifier of the leaderboard.</param>
    /// <param name="cancellationToken">Optional. A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ClearLeaderboardAsync(object leaderboardId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the leaderboard by provided <para>leaderboardId</para>. If the leaderboard does not exist, nothing happens.
    /// </summary>
    /// <param name="leaderboardId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task DeleteLeaderboardAsync(
        object leaderboardId,
        CancellationToken cancellationToken = default);
}