using Redisboard.NET.Enumerations;
using StackExchange.Redis;

namespace Redisboard.NET.Interfaces;

/// <summary>
/// Interface for interacting with the leaderboard
/// </summary>
public interface ILeaderboard
{
    /// <summary>
    /// Asynchronously adds the specified entity to the leaderboard with score of 0
    /// </summary>
    /// <param name="leaderboardKey">Unique identifier for the leaderboard</param>
    /// <param name="entity">The entity to add to the leaderboard.</param>
    /// <param name="fireAndForget">
    /// If set to <c>true</c>, the operation will be executed without waiting for a response from Redis.
    /// This can improve performance but provides no guarantee that the operation was successful.
    /// </param>
    Task AddEntityAsync(
        RedisValue leaderboardKey,
        ILeaderboardEntity entity,
        bool fireAndForget = false);

    /// <summary>
    /// Asynchronously updates the score of an entity in the leaderboard.
    /// </summary>
    /// <param name="leaderboardKey">The key identifying the leaderboard.</param>
    /// <param name="entityKey">The unique identifier of the entity whose score is being updated.</param>
    /// <param name="newScore">The new score to be assigned to the entity.</param>
    /// <param name="fireAndForget">
    /// If set to <c>true</c>, the operation will be executed without waiting for a response from Redis.
    /// This can improve performance but provides no guarantee that the operation was successful.
    /// </param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the leaderboardKey, entityKey, or newScore is invalid.
    /// </exception>
    Task UpdateEntityScoreAsync(
        RedisValue leaderboardKey,
        RedisValue entityKey,
        double newScore,
        bool fireAndForget = false);

    /// <summary>
    /// Retrieves a leaderboard entity by its unique identifier, including its neighbors with a defined offset.
    /// </summary>
    /// <param name="leaderboardKey">Unique identifier for the leaderboard</param>
    /// <param name="entityKey">The unique identifier of the leaderboard entity.</param>
    /// <param name="offset">The number of neighbours to retrieve before and after the current entity</param>
    /// <param name="rankingType">The ranking type to use for ordering the leaderboard.</param>
    /// <returns>An array of leaderboard entities with the requested member in the middle.</returns>
    Task<ILeaderboardEntity[]> GetEntityAndNeighboursAsync(
        RedisValue leaderboardKey,
        RedisValue entityKey,
        int offset = 10,
        RankingType rankingType = RankingType.Default);

    /// <summary>
    /// Retrieves a subset of the leaderboard based on score range.
    /// </summary>
    /// <param name="leaderboardKey">Unique identifier for the leaderboard</param>
    /// <param name="minScore">The minimum score value (inclusive).</param>
    /// <param name="maxScore">The maximum score value (inclusive).</param>
    /// <param name="rankingType">The ranking type to use for ordering the leaderboard.</param>
    /// <returns>A subset of the leaderboard based on score range.</returns>
    Task<ILeaderboardEntity[]> GetEntitiesByScoreRangeAsync(
        RedisValue leaderboardKey,
        double minScore,
        double maxScore,
        RankingType rankingType = RankingType.Default);

    // /// <summary>
    // /// Retrieves the data of a specific entity from the leaderboard.
    // /// </summary>
    // /// <param name="leaderboardKey">The identifier of the leaderboard.</param>
    // /// <param name="entityKey">The identifier of the entity whose data is to be retrieved.</param>
    // /// <returns>A task that represents the asynchronous operation. The task result contains the entity data.</returns>
    // Task<TEntity> GetEntityDataAsync(
    //     RedisValue leaderboardKey,
    //     RedisValue entityKey);

    /// <summary>
    /// Retrieves the score of a specific entity from the leaderboard asynchronously.
    /// </summary>
    /// <param name="leaderboardKey">The identifier of the leaderboard.</param>
    /// <param name="entityKey">The identifier of the entity whose score is to be retrieved.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the score of the entity.</returns>
    Task<double?> GetEntityScoreAsync(
        RedisValue leaderboardKey,
        RedisValue entityKey);

    /// <summary>
    /// Retrieves the rank of a specific entity in the leaderboard asynchronously.
    /// </summary>
    /// <param name="leaderboardKey"></param>
    /// <param name="entityKey">The identifier of the entity whose rank is to be retrieved.</param>
    /// <param name="rankingType"></param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the rank of the entity.</returns>
    Task<long?> GetEntityRankAsync(
        RedisValue leaderboardKey, 
        RedisValue entityKey,
        RankingType rankingType = RankingType.Default);

    /// <summary>
    /// Removes a specific entity from the leaderboard.
    /// </summary>
    /// <param name="leaderboardKey"></param>
    /// <param name="entityKey">The identifier of the entity to be removed.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task DeleteEntityAsync(
        RedisValue leaderboardKey, 
        RedisValue entityKey);

    /// <summary>
    /// Retrieves the size of the leaderboard.
    /// </summary>
    /// <param name="leaderboardKey">The identifier of the leaderboard.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the size of the leaderboard.</returns>
    Task<long> GetSizeAsync(RedisValue leaderboardKey);

    /// <summary>
    /// Deletes the leaderboard by provided <param>leaderboardKey</param>. If the leaderboard does not exist, nothing happens.
    /// </summary>
    /// <param name="leaderboardKey"></param>
    /// <returns></returns>
    Task DeleteAsync(RedisValue leaderboardKey);
}