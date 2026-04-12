using Redisboard.NET.Enumerations;
using StackExchange.Redis;

namespace Redisboard.NET.Interfaces;

/// <summary>
/// Defines leaderboard operations for entities stored in Redis.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ILeaderboard{TEntity}"/> exposes a strongly typed facade over Redis sorted sets and
/// metadata hashes so application code can manage ranked entities without manual serialization or
/// Redis command orchestration.
/// </para>
/// <para>
/// Implementations treat higher scores as better ranks even though values are stored inverted in Redis.
/// Returned entities should always have <see cref="ILeaderboardEntity.Rank"/> populated according to the
/// requested <see cref="RankingType"/>.
/// </para>
/// </remarks>
/// <typeparam name="TEntity">
/// Entity type stored in leaderboard. It must implement <see cref="ILeaderboardEntity"/>, expose a
/// parameterless constructor, and declare exactly one key property and one score property by using
/// <see cref="Attributes.LeaderboardKeyAttribute"/> and <see cref="Attributes.LeaderboardScoreAttribute"/>.
/// </typeparam>
public interface ILeaderboard<TEntity>
    where TEntity : ILeaderboardEntity, new()
{
    /// <summary>
    /// Adds an entity to a leaderboard.
    /// </summary>
    /// <param name="leaderboardKey">Identifies leaderboard whose ranking and metadata structures should receive <paramref name="entity"/>.</param>
    /// <param name="entity">Entity to add. Its configured key and score properties supply the Redis member identifier and ranking score.</param>
    /// <param name="fireAndForget">When <see langword="true"/>, sends the write without waiting for Redis to acknowledge completion.</param>
    /// <param name="cancellationToken">Cancels operation before Redis script execution begins.</param>
    /// <remarks>
    /// Writes ranking data, score-tracking data, and serialized metadata for <paramref name="entity"/>.
    /// </remarks>
    Task AddEntityAsync(
        RedisValue leaderboardKey,
        TEntity entity,
        bool fireAndForget = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds multiple entities to a leaderboard in one batch operation.
    /// </summary>
    /// <param name="leaderboardKey">Identifies leaderboard whose ranking and metadata structures should receive <paramref name="entities"/>.</param>
    /// <param name="entities">Entities to add. Each entry must provide a valid key and non-negative score.</param>
    /// <param name="fireAndForget">When <see langword="true"/>, sends the write without waiting for Redis to acknowledge completion.</param>
    /// <param name="cancellationToken">Cancels operation before Redis script execution begins.</param>
    /// <remarks>
    /// Batch writes reduce round trips and should be preferred when adding many entities at once.
    /// Implementations may reject batches larger than <see cref="Leaderboard{TEntity}.DefaultMaxBatchOperationSize"/>
    /// to keep Redis script payloads bounded.
    /// </remarks>
    Task AddEntitiesAsync(
        RedisValue leaderboardKey,
        IEnumerable<TEntity> entities,
        bool fireAndForget = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the score of an existing entity without rewriting its metadata.
    /// </summary>
    /// <param name="leaderboardKey">Identifies leaderboard that contains <paramref name="entity"/>.</param>
    /// <param name="entity">Entity whose configured key identifies the stored member and whose score property contains the replacement score.</param>
    /// <param name="fireAndForget">When <see langword="true"/>, sends the write without waiting for Redis to acknowledge completion.</param>
    /// <param name="cancellationToken">Cancels operation before Redis script execution begins.</param>
    /// <remarks>
    /// Call <see cref="UpdateEntityMetadataAsync(StackExchange.Redis.RedisValue,TEntity,bool,System.Threading.CancellationToken)"/>
    /// separately when non-score properties must also be persisted.
    /// </remarks>
    Task UpdateEntityScoreAsync(
        RedisValue leaderboardKey,
        TEntity entity,
        bool fireAndForget = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the stored metadata for an existing entity without changing its score.
    /// </summary>
    /// <param name="leaderboardKey">Identifies leaderboard that contains <paramref name="entity"/>.</param>
    /// <param name="entity">Entity whose configured key identifies the stored member and whose serialized state becomes the new metadata payload.</param>
    /// <param name="fireAndForget">When <see langword="true"/>, sends the write without waiting for Redis to acknowledge completion.</param>
    /// <param name="cancellationToken">Cancels operation before Redis hash update begins.</param>
    /// <remarks>
    /// Ranking order does not change. Use
    /// <see cref="UpdateEntityScoreAsync(StackExchange.Redis.RedisValue,TEntity,bool,System.Threading.CancellationToken)"/>
    /// when score changes must also be persisted.
    /// </remarks>
    Task UpdateEntityMetadataAsync(
        RedisValue leaderboardKey,
        TEntity entity,
        bool fireAndForget = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns an entity together with neighboring entities around its current rank.
    /// </summary>
    /// <param name="leaderboardKey">Identifies leaderboard to query.</param>
    /// <param name="entityKey">Identifies entity whose neighborhood should be returned.</param>
    /// <param name="offset">Maximum number of entities to include above and below target entity. Cannot be negative.</param>
    /// <param name="rankingType">Ranking algorithm used to populate <see cref="ILeaderboardEntity.Rank"/> on returned entities.</param>
    /// <param name="cancellationToken">Cancels operation before or during downstream leaderboard reads.</param>
    /// <returns>Array containing target entity and nearby entities ordered by rank, or an empty array when target entity does not exist.</returns>
    Task<TEntity[]> GetEntityAndNeighboursAsync(
        RedisValue leaderboardKey,
        RedisValue entityKey,
        int offset = 10,
        RankingType rankingType = RankingType.Default,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns entities whose scores fall within an inclusive score range.
    /// </summary>
    /// <param name="leaderboardKey">Identifies leaderboard to query.</param>
    /// <param name="minScore">Inclusive lower bound for score filter. Cannot be negative.</param>
    /// <param name="maxScore">Inclusive upper bound for score filter. Cannot be negative and must be greater than or equal to <paramref name="minScore"/>.</param>
    /// <param name="rankingType">Ranking algorithm used to populate <see cref="ILeaderboardEntity.Rank"/> on returned entities.</param>
    /// <param name="cancellationToken">Cancels operation before or during downstream leaderboard reads.</param>
    /// <returns>Entities that match requested score range, ordered by rank, or an empty array when no entries match.</returns>
    Task<TEntity[]> GetEntitiesByScoreRangeAsync(
        RedisValue leaderboardKey,
        double minScore,
        double maxScore,
        RankingType rankingType = RankingType.Default,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns entities whose ranks fall within an inclusive rank range.
    /// </summary>
    /// <param name="leaderboardKey">Identifies leaderboard to query.</param>
    /// <param name="startRank">One-based starting rank for page.</param>
    /// <param name="endRank">One-based ending rank for page. Must be greater than or equal to <paramref name="startRank"/>.</param>
    /// <param name="rankingType">Ranking algorithm used to populate <see cref="ILeaderboardEntity.Rank"/> on returned entities.</param>
    /// <param name="cancellationToken">Cancels operation before or during leaderboard reads.</param>
    /// <returns>Entities within requested rank range ordered by rank.</returns>
    Task<TEntity[]> GetEntitiesByRankRangeAsync(
        RedisValue leaderboardKey,
        long startRank,
        long endRank,
        RankingType rankingType = RankingType.Default,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns current score for a single entity.
    /// </summary>
    /// <param name="leaderboardKey">Identifies leaderboard to query.</param>
    /// <param name="entityKey">Identifies entity whose score should be returned.</param>
    /// <param name="cancellationToken">Propagates cancellation for API consumers.</param>
    /// <returns>Normalized score stored for entity, or <see langword="null"/> when entity does not exist.</returns>
    Task<double?> GetEntityScoreAsync(
        RedisValue leaderboardKey,
        RedisValue entityKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns current rank for a single entity.
    /// </summary>
    /// <param name="leaderboardKey">Identifies leaderboard to query.</param>
    /// <param name="entityKey">Identifies entity whose rank should be returned.</param>
    /// <param name="rankingType">Ranking algorithm used to compute returned rank.</param>
    /// <param name="cancellationToken">Cancels operation before or during downstream leaderboard reads.</param>
    /// <returns>One-based rank for entity, or <see langword="null"/> when entity does not exist.</returns>
    Task<long?> GetEntityRankAsync(
        RedisValue leaderboardKey,
        RedisValue entityKey,
        RankingType rankingType = RankingType.Default,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns number of entities currently stored in a leaderboard.
    /// </summary>
    /// <param name="leaderboardKey">Identifies leaderboard to query.</param>
    /// <param name="cancellationToken">Propagates cancellation for API consumers.</param>
    /// <returns>Total number of entities with metadata stored for <paramref name="leaderboardKey"/>.</returns>
    Task<long> GetSizeAsync(
        RedisValue leaderboardKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a single entity from a leaderboard.
    /// </summary>
    /// <param name="leaderboardKey">Identifies leaderboard that contains entity to remove.</param>
    /// <param name="entityKey">Identifies entity to delete.</param>
    /// <param name="cancellationToken">Cancels operation before Redis script execution begins.</param>
    /// <remarks>
    /// Removes ranking data, score-tracking data, and metadata for supplied entity.
    /// </remarks>
    Task DeleteEntityAsync(
        RedisValue leaderboardKey,
        RedisValue entityKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes multiple entities from a leaderboard in one batch operation.
    /// </summary>
    /// <param name="leaderboardKey">Identifies leaderboard that contains entities to remove.</param>
    /// <param name="entityKeys">Keys for entities to delete. Collection must contain at least one value.</param>
    /// <param name="cancellationToken">Cancels operation before Redis script execution begins.</param>
    /// <remarks>
    /// Batch deletion removes ranking data and metadata for all supplied entity keys in one Redis script call.
    /// Implementations may reject batches larger than <see cref="Leaderboard{TEntity}.DefaultMaxBatchOperationSize"/>
    /// to keep Redis script payloads bounded.
    /// </remarks>
    Task DeleteEntitiesAsync(
        RedisValue leaderboardKey,
        IEnumerable<RedisValue> entityKeys,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all Redis data associated with a leaderboard.
    /// </summary>
    /// <param name="leaderboardKey">Identifies leaderboard to delete.</param>
    /// <param name="cancellationToken">Propagates cancellation for API consumers.</param>
    /// <remarks>
    /// Removes ranking data, metadata, and auxiliary score tracking keys for supplied leaderboard.
    /// </remarks>
    Task DeleteAsync(
        RedisValue leaderboardKey,
        CancellationToken cancellationToken = default);
}
