using Redisboard.NET.Enumerations;

namespace Redisboard.NET.Interfaces;

/// <summary>
/// Interface for interacting with the leaderboard
/// </summary>
/// <typeparam name="TEntity">The type of leaderboard entities.</typeparam>
public interface ILeaderboard<TEntity>
    where TEntity : ILeaderboardEntity
{
    /// <summary>
    /// Adds all the specified entities with the specified scores to the leaderboard at key.
    /// If a specified entity is already a member of the leaderboard, only the score is updated and the entity reinserted at the right position to ensure the correct ordering.
    /// </summary>
    /// <param name="leaderboardId">Unique identifier for the leaderboard</param>
    /// <param name="entities">The entity/entities to add to the leaderboard.</param>
    /// <param name="fireAndForget">Utilizes a fire-and-forget approach to saving data. It has an improved performance over waiting for the response from the server, however keep in mind that you won't notice any server errors that get reported.</param>
    Task AddEntitiesAsync(
        object leaderboardId,
        TEntity[] entities,
        bool fireAndForget = default);
    
    /// <summary>
    /// Retrieves a leaderboard entity by its unique identifier, including its neighbors with a defined offset.
    /// </summary>
    /// <param name="leaderboardId">Unique identifier for the leaderboard</param>
    /// <param name="entityId">The unique identifier of the leaderboard entity.</param>
    /// <param name="offset">The number of neighbours to retrieve before and after the current entity</param>
    /// <param name="rankingType">The ranking type to use for ordering the leaderboard.</param>
    /// <returns>An array of leaderboard entities with the requested member in the middle.</returns>
    Task<TEntity[]> GetEntityAndNeighboursAsync(
        object leaderboardId,
        string entityId,
        int offset = 10,
        RankingType rankingType = RankingType.Default);

    /// <summary>
    /// Retrieves a subset of the leaderboard based on score range.
    /// </summary>
    /// <param name="leaderboardId">Unique identifier for the leaderboard</param>
    /// <param name="minScore">The minimum score value (inclusive).</param>
    /// <param name="maxScore">The maximum score value (inclusive).</param>
    /// <param name="rankingType">The ranking type to use for ordering the leaderboard.</param>
    /// <returns>A subset of the leaderboard based on score range.</returns>
    Task<TEntity[]> GetEntitiesByScoreRangeAsync(
        object leaderboardId,
        double minScore,
        double maxScore,
        RankingType rankingType = RankingType.Default);

    /// <summary>
    /// Retrieves the data of a specific entity from the leaderboard.
    /// </summary>
    /// <param name="leaderboardId">The identifier of the leaderboard.</param>
    /// <param name="entityId">The identifier of the entity whose data is to be retrieved.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the entity data.</returns>
    Task<TEntity> GetEntityDataAsync(
        object leaderboardId, 
        string entityId);

    /// <summary>
    /// Retrieves the score of a specific entity from the leaderboard asynchronously.
    /// </summary>
    /// <param name="leaderboardId">The identifier of the leaderboard.</param>
    /// <param name="entityId">The identifier of the entity whose score is to be retrieved.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the score of the entity.</returns>
    Task<double> GetEntityScoreAsync(
        object leaderboardId, 
        string entityId);

    /// <summary>
    /// Retrieves the rank of a specific entity in the leaderboard asynchronously.
    /// </summary>
    /// <param name="entityId">The identifier of the entity whose rank is to be retrieved.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the rank of the entity.</returns>
    Task<long> GetEntityRankAsync(string entityId);

    /// <summary>
    /// Removes a specific entity from the leaderboard.
    /// </summary>
    /// <param name="entityId">The identifier of the entity to be removed.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task DeleteEntityAsync(string entityId);

    /// <summary>
    /// Retrieves the size of the leaderboard.
    /// </summary>
    /// <param name="leaderboardId">The identifier of the leaderboard.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the size of the leaderboard.</returns>
    Task<long> GetSizeAsync(object leaderboardId);

    /// <summary>
    /// Clears all entries from the leaderboard.
    /// </summary>
    /// <param name="leaderboardId">The identifier of the leaderboard.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ClearAsync(object leaderboardId);

    /// <summary>
    /// Deletes the leaderboard by provided <param>leaderboardId</param>. If the leaderboard does not exist, nothing happens.
    /// </summary>
    /// <param name="leaderboardId"></param>
    /// <returns></returns>
    Task DeleteAsync(object leaderboardId);
}