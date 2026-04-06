using Redisboard.NET.Enumerations;
using StackExchange.Redis;

namespace Redisboard.NET.Interfaces;

/// <summary>
/// A ranked leaderboard backed by a Redis sorted set.
///
/// Use this interface to store, update, and query entities that have a natural ranking
/// (e.g., players by score, items by rating). All methods accept or return your strongly-typed
/// domain entity — no manual serialization, key construction, or Redis command syntax required.
///
/// <para>
/// <typeparamref name="TEntity"/> must:
/// <list type="bullet">
///   <item>Implement <see cref="ILeaderboardEntity"/></item>
///   <item>Expose a parameterless constructor</item>
///   <item>Decorate exactly one property with <see cref="Attributes.LeaderboardKeyAttribute"/></item>
///   <item>Decorate exactly one property with <see cref="Attributes.LeaderboardScoreAttribute"/></item>
/// </list>
/// </para>
///
/// <para>
/// Score is stored inverted (negated) in Redis to achieve descending sort via the native
/// sorted set ordering. All ranking methods return ranks in the correct descending order
/// (highest score = rank 1).
/// </para>
/// </summary>
/// <typeparam name="TEntity">The domain type stored in the leaderboard.</typeparam>
public interface ILeaderboard<TEntity>
    where TEntity : ILeaderboardEntity, new()
{
    /// <summary>
    /// Adds a new entity to the leaderboard.
    /// </summary>
    /// <param name="leaderboardKey">Identifies the leaderboard (e.g., "game:session:42").</param>
    /// <param name="entity">The entity to add. Its key property identifies it uniquely; its score property sets its initial ranking.</param>
    /// <param name="fireAndForget">If <c>true</c>, the operation returns immediately without waiting for Redis confirmation. Use for high-throughput write-once scenarios where delivery is not critical.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be cancelled.</param>
    /// <remarks>
    /// The entity's <see cref="Attributes.LeaderboardKeyAttribute"/>-annotated property is used as the unique identifier.
    /// The <see cref="Attributes.LeaderboardScoreAttribute"/>-annotated property provides the initial score.
    /// All other public properties are serialized as JSON and stored as metadata in a Redis hash.
    /// </remarks>
    Task AddEntityAsync(
        RedisValue leaderboardKey,
        TEntity entity,
        bool fireAndForget = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates only the score of an entity that already exists in the leaderboard.
    /// </summary>
    /// <param name="leaderboardKey">Identifies the leaderboard.</param>
    /// <param name="entity">The entity with the updated score. Its key property identifies which entity to update.</param>
    /// <param name="fireAndForget">If <c>true</c>, the operation returns immediately without waiting for Redis confirmation.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be cancelled.</param>
    /// <remarks>
    /// Metadata stored for the entity is left unchanged. To update non-score properties, use <see cref="UpdateEntityMetadataAsync"/>.
    /// </remarks>
    Task UpdateEntityScoreAsync(
        RedisValue leaderboardKey,
        TEntity entity,
        bool fireAndForget = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates only the metadata (non-score properties) of an existing entity.
    /// </summary>
    /// <param name="leaderboardKey">Identifies the leaderboard.</param>
    /// <param name="entity">The entity with updated metadata. Its key property identifies which entity to update.</param>
    /// <param name="fireAndForget">If <c>true</c>, the operation returns immediately without waiting for Redis confirmation.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be cancelled.</param>
    /// <remarks>
    /// The entity's score in the leaderboard is not affected. Re-serializes all public properties (except the key and score) as JSON and replaces the stored metadata.
    /// </remarks>
    Task UpdateEntityMetadataAsync(
        RedisValue leaderboardKey,
        TEntity entity,
        bool fireAndForget = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a specific entity along with the entities surrounding it (above and below in rank).
    /// </summary>
    /// <param name="leaderboardKey">Identifies the leaderboard.</param>
    /// <param name="entityKey">The unique key of the entity to look up.</param>
    /// <param name="offset">The number of entities to include on each side of the target entity. Default is 10.</param>
    /// <param name="rankingType">The ranking scheme to use when computing ranks. Default is standard ranking.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be cancelled.</param>
    /// <returns>
    /// An array containing the target entity and up to <paramref name="offset"/> entities above and below it,
    /// sorted by rank (highest score first). Returns an empty array if the entity is not found.
    /// </returns>
    /// <remarks>
    /// The returned entities have their <see cref="ILeaderboardEntity.Rank"/> property populated.
    /// </remarks>
    Task<TEntity[]> GetEntityAndNeighboursAsync(
        RedisValue leaderboardKey,
        RedisValue entityKey,
        int offset = 10,
        RankingType rankingType = RankingType.Default,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all entities whose scores fall within a specified range.
    /// </summary>
    /// <param name="leaderboardKey">Identifies the leaderboard.</param>
    /// <param name="minScore">The minimum score (inclusive). Higher scores rank first.</param>
    /// <param name="maxScore">The maximum score (inclusive). Higher scores rank first.</param>
    /// <param name="rankingType">The ranking scheme to use when computing ranks.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be cancelled.</param>
    /// <returns>An array of matching entities sorted by rank, each with their <see cref="ILeaderboardEntity.Rank"/> populated. Empty if no entities match.</returns>
    Task<TEntity[]> GetEntitiesByScoreRangeAsync(
        RedisValue leaderboardKey,
        double minScore,
        double maxScore,
        RankingType rankingType = RankingType.Default,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a page of entities by their rank position.
    /// </summary>
    /// <param name="leaderboardKey">Identifies the leaderboard.</param>
    /// <param name="startRank">The 1-based rank of the first entity to retrieve (e.g., 1 for the top entity).</param>
    /// <param name="endRank">The 1-based rank of the last entity to retrieve (inclusive).</param>
    /// <param name="rankingType">The ranking scheme to use when computing ranks.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be cancelled.</param>
    /// <returns>An array of entities within the rank range, sorted by rank. Each entity's <see cref="ILeaderboardEntity.Rank"/> is populated.</returns>
    /// <remarks>
    /// Ranks are 1-based: rank 1 is the highest-scoring entity, rank 2 is second, and so on.
    /// </remarks>
    Task<TEntity[]> GetEntitiesByRankRangeAsync(
        RedisValue leaderboardKey,
        long startRank,
        long endRank,
        RankingType rankingType = RankingType.Default,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current score of a specific entity.
    /// </summary>
    /// <param name="leaderboardKey">Identifies the leaderboard.</param>
    /// <param name="entityKey">The unique key of the entity.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be cancelled.</param>
    /// <returns>The entity's score, or <c>null</c> if the entity does not exist in the leaderboard.</returns>
    Task<double?> GetEntityScoreAsync(
        RedisValue leaderboardKey,
        RedisValue entityKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current rank of a specific entity.
    /// </summary>
    /// <param name="leaderboardKey">Identifies the leaderboard.</param>
    /// <param name="entityKey">The unique key of the entity.</param>
    /// <param name="rankingType">The ranking scheme to use when computing the rank.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be cancelled.</param>
    /// <returns>The entity's 1-based rank (1 = highest score), or <c>null</c> if the entity is not found.</returns>
    Task<long?> GetEntityRankAsync(
        RedisValue leaderboardKey,
        RedisValue entityKey,
        RankingType rankingType = RankingType.Default,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total number of entities in the leaderboard.
    /// </summary>
    /// <param name="leaderboardKey">Identifies the leaderboard.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be cancelled.</param>
    /// <returns>The count of entities currently stored.</returns>
    Task<long> GetSizeAsync(
        RedisValue leaderboardKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a specific entity from the leaderboard.
    /// </summary>
    /// <param name="leaderboardKey">Identifies the leaderboard.</param>
    /// <param name="entityKey">The unique key of the entity to remove.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be cancelled.</param>
    /// <remarks>
    /// This deletes both the entity's entry in the sorted set and its stored metadata.
    /// </remarks>
    Task DeleteEntityAsync(
        RedisValue leaderboardKey,
        RedisValue entityKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently deletes the entire leaderboard and all associated data.
    /// </summary>
    /// <param name="leaderboardKey">Identifies the leaderboard to delete.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be cancelled.</param>
    /// <remarks>
    /// This operation deletes the sorted set, the metadata hash, and the unique score set associated with this leaderboard.
    /// This action is irreversible.
    /// </remarks>
    Task DeleteAsync(
        RedisValue leaderboardKey,
        CancellationToken cancellationToken = default);
}
