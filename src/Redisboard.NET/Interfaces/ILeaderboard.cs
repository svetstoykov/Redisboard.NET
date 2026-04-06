using Redisboard.NET.Enumerations;
using StackExchange.Redis;

namespace Redisboard.NET.Interfaces;

/// <summary>
/// Strongly-typed leaderboard interface.
/// All write methods accept <typeparamref name="TEntity"/> directly;
/// all read methods return <typeparamref name="TEntity"/>[] fully populated — no manual
/// serialization, deserialization, or key management required.
/// </summary>
/// <typeparam name="TEntity">
/// The domain type stored in the leaderboard.
/// Must implement <see cref="ILeaderboardEntity"/>, expose a parameterless constructor,
/// and decorate exactly one property with <see cref="Attributes.LeaderboardKeyAttribute"/>
/// and exactly one with <see cref="Attributes.LeaderboardScoreAttribute"/>.
/// </typeparam>
public interface ILeaderboard<TEntity>
    where TEntity : ILeaderboardEntity, new()
{
    /// <summary>
    /// Adds an entity to the leaderboard.
    /// The entity key and initial score are read from the properties annotated with
    /// <see cref="Attributes.LeaderboardKeyAttribute"/> and <see cref="Attributes.LeaderboardScoreAttribute"/>.
    /// All remaining public properties are serialized and stored as metadata.
    /// </summary>
    Task AddEntityAsync(
        RedisValue leaderboardKey,
        TEntity entity,
        bool fireAndForget = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the score of an existing entity.
    /// The entity key and new score are read from the entity's annotated properties.
    /// </summary>
    Task UpdateEntityScoreAsync(
        RedisValue leaderboardKey,
        TEntity entity,
        bool fireAndForget = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-serializes the entity and replaces its stored metadata.
    /// Use this when non-score properties on the entity have changed.
    /// </summary>
    Task UpdateEntityMetadataAsync(
        RedisValue leaderboardKey,
        TEntity entity,
        bool fireAndForget = false,
        CancellationToken cancellationToken = default);

    // ---- Read ------------------------------------------------------------------

    /// <summary>
    /// Retrieves the entity with the given key together with its neighbours,
    /// returning <typeparamref name="TEntity"/>[] fully populated.
    /// </summary>
    Task<TEntity[]> GetEntityAndNeighboursAsync(
        RedisValue leaderboardKey,
        RedisValue entityKey,
        int offset = 10,
        RankingType rankingType = RankingType.Default,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all entities whose score falls within [<paramref name="minScore"/>,
    /// <paramref name="maxScore"/>], fully populated.
    /// </summary>
    Task<TEntity[]> GetEntitiesByScoreRangeAsync(
        RedisValue leaderboardKey,
        double minScore,
        double maxScore,
        RankingType rankingType = RankingType.Default,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the page of entities at the specified 1-indexed rank range, fully populated.
    /// </summary>
    Task<TEntity[]> GetEntitiesByRankRangeAsync(
        RedisValue leaderboardKey,
        long startRank,
        long endRank,
        RankingType rankingType = RankingType.Default,
        CancellationToken cancellationToken = default);

    // ---- Utility ---------------------------------------------------------------

    /// <summary>Returns the current score of the entity, or <c>null</c> if not found.</summary>
    Task<double?> GetEntityScoreAsync(
        RedisValue leaderboardKey,
        RedisValue entityKey,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the current rank of the entity, or <c>null</c> if not found.</summary>
    Task<long?> GetEntityRankAsync(
        RedisValue leaderboardKey,
        RedisValue entityKey,
        RankingType rankingType = RankingType.Default,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the total number of entities in the leaderboard.</summary>
    Task<long> GetSizeAsync(
        RedisValue leaderboardKey,
        CancellationToken cancellationToken = default);

    /// <summary>Removes the specified entity from the leaderboard.</summary>
    Task DeleteEntityAsync(
        RedisValue leaderboardKey,
        RedisValue entityKey,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes the entire leaderboard and all associated data.</summary>
    Task DeleteAsync(
        RedisValue leaderboardKey,
        CancellationToken cancellationToken = default);
}
