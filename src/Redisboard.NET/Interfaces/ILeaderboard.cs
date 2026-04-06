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
    /// <param name="entityKey">Unique identifier for the entity</param>
    /// <param name="metadata">Optional metadata to associate with the entity.</param>
    /// <param name="fireAndForget">
    /// If set to <c>true</c>, the operation will be executed without waiting for a response from Redis.
    /// This can improve performance but provides no guarantee that the operation was successful.
    /// </param>
    /// <param name="cancellationToken">A cancellation token to observe.</param>
    Task AddEntityAsync(RedisValue leaderboardKey, RedisValue entityKey, RedisValue metadata = default, bool fireAndForget = false, CancellationToken cancellationToken = default);

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
    /// <param name="cancellationToken">A cancellation token to observe.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the leaderboardKey, entityKey, or newScore is invalid.
    /// </exception>
    Task UpdateEntityScoreAsync(RedisValue leaderboardKey, RedisValue entityKey, double newScore, bool fireAndForget = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously updates the metadata of an entity in the leaderboard.
    /// </summary>
    /// <param name="leaderboardKey">The key identifying the leaderboard.</param>
    /// <param name="entityKey">The unique identifier of the entity whose score is being updated.</param>
    /// <param name="metadata">The new metadata to be assigned to the entity.</param>
    /// <param name="fireAndForget">
    /// If set to <c>true</c>, the operation will be executed without waiting for a response from Redis.
    /// This can improve performance but provides no guarantee that the operation was successful.
    /// </param>
    /// <param name="cancellationToken">A cancellation token to observe.</param>
    /// <exception cref="Exception">
    /// Thrown when the leaderboardKey, entityKey, or metadata is invalid.
    /// </exception>
    Task UpdateEntityMetadataAsync(RedisValue leaderboardKey, RedisValue entityKey, RedisValue metadata, bool fireAndForget = false, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieves a leaderboard entity by its unique identifier, including its neighbors with a defined offset.
    /// </summary>
    /// <param name="leaderboardKey">Unique identifier for the leaderboard</param>
    /// <param name="entityKey">The unique identifier of the leaderboard entity.</param>
    /// <param name="offset">The number of neighbours to retrieve before and after the current entity</param>
    /// <param name="rankingType">The ranking type to use for ordering the leaderboard.</param>
    /// <param name="cancellationToken">A cancellation token to observe.</param>
    /// <returns>An array of leaderboard entities with the requested member in the middle.</returns>
    Task<ILeaderboardEntity[]> GetEntityAndNeighboursAsync(RedisValue leaderboardKey, RedisValue entityKey, int offset = 10, RankingType rankingType = RankingType.Default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a subset of the leaderboard based on score range.
    /// </summary>
    /// <param name="leaderboardKey">Unique identifier for the leaderboard</param>
    /// <param name="minScore">The minimum score value (inclusive).</param>
    /// <param name="maxScore">The maximum score value (inclusive).</param>
    /// <param name="rankingType">The ranking type to use for ordering the leaderboard.</param>
    /// <param name="cancellationToken">A cancellation token to observe.</param>
    /// <returns>A subset of the leaderboard based on score range.</returns>
    Task<ILeaderboardEntity[]> GetEntitiesByScoreRangeAsync(RedisValue leaderboardKey, double minScore, double maxScore, RankingType rankingType = RankingType.Default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the score of a specific entity from the leaderboard asynchronously.
    /// </summary>
    /// <param name="leaderboardKey">The identifier of the leaderboard.</param>
    /// <param name="entityKey">The identifier of the entity whose score is to be retrieved.</param>
    /// <param name="cancellationToken">A cancellation token to observe.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the score of the entity.</returns>
    Task<double?> GetEntityScoreAsync(RedisValue leaderboardKey, RedisValue entityKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the rank of a specific entity in the leaderboard asynchronously.
    /// </summary>
    /// <param name="leaderboardKey"></param>
    /// <param name="entityKey">The identifier of the entity whose rank is to be retrieved.</param>
    /// <param name="rankingType"></param>
    /// <param name="cancellationToken">A cancellation token to observe.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the rank of the entity.</returns>
    Task<long?> GetEntityRankAsync(RedisValue leaderboardKey, RedisValue entityKey, RankingType rankingType = RankingType.Default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a specific entity from the leaderboard.
    /// </summary>
    /// <param name="leaderboardKey"></param>
    /// <param name="entityKey">The identifier of the entity to be removed.</param>
    /// <param name="cancellationToken">A cancellation token to observe.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task DeleteEntityAsync(RedisValue leaderboardKey, RedisValue entityKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the size of the leaderboard.
    /// </summary>
    /// <param name="leaderboardKey">The identifier of the leaderboard.</param>
    /// <param name="cancellationToken">A cancellation token to observe.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the size of the leaderboard.</returns>
    Task<long> GetSizeAsync(RedisValue leaderboardKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the leaderboard by provided <param>leaderboardKey</param>. If the leaderboard does not exist, nothing happens.
    /// </summary>
    /// <param name="leaderboardKey"></param>
    /// <param name="cancellationToken">A cancellation token to observe.</param>
    /// <returns></returns>
    Task DeleteAsync(RedisValue leaderboardKey, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieves a page of leaderboard entries by rank range.
    /// Ranks are 1-indexed (rank 1 is the top player), consistent with <see cref="GetEntityRankAsync"/>.
    /// If the requested range exceeds the leaderboard size, only the existing entries are returned.
    /// </summary>
    /// <param name="leaderboardKey">Unique identifier for the leaderboard.</param>
    /// <param name="startRank">The starting rank (inclusive, 1-indexed). Must be >= 1.</param>
    /// <param name="endRank">The ending rank (inclusive, 1-indexed). Must be >= startRank.</param>
    /// <param name="rankingType">The ranking type to use for ordering the leaderboard.</param>
    /// <param name="cancellationToken">A cancellation token to observe.</param>
    /// <returns>An array of leaderboard entities within the specified rank range.</returns>
    Task<ILeaderboardEntity[]> GetEntitiesByRankRangeAsync(
        RedisValue leaderboardKey,
        long startRank,
        long endRank,
        RankingType rankingType = RankingType.Default,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the metadata associated with a specific entity in the leaderboard.
    /// </summary>
    /// <param name="leaderboardKey">The unique identifier for the leaderboard.</param>
    /// <param name="entityKey">The unique identifier for the entity within the leaderboard.</param>
    /// <param name="cancellationToken">A cancellation token to observe.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the metadata
    /// of the entity as a RedisValue. If the entity is not found, the result will be RedisValue.Null.
    /// </returns>
    Task<RedisValue> GetEntityMetadataAsync(RedisValue leaderboardKey, RedisValue entityKey, CancellationToken cancellationToken = default);
}