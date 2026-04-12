namespace Redisboard.NET.Interfaces;

/// <summary>
/// Represents an entity that can participate in a leaderboard.
/// </summary>
/// <remarks>
/// <para>
/// Implementations must declare exactly one property marked with
/// <see cref="Redisboard.NET.Attributes.LeaderboardKeyAttribute"/> and exactly one property marked with
/// <see cref="Redisboard.NET.Attributes.LeaderboardScoreAttribute"/> so leaderboard operations can map the
/// entity to Redis keys and scores.
/// </para>
/// <para>
/// <see cref="Rank"/> is populated by leaderboard read operations. Implementations should treat it as
/// library-managed output rather than user-supplied input.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class Player : ILeaderboardEntity
/// {
///     [LeaderboardKey]
///     public string Id { get; set; }
///
///     [LeaderboardScore]
///     public double Points { get; set; }
///
///     public long Rank { get; set; }
///
///     public string Username { get; set; }
/// }
/// </code>
/// </example>
public interface ILeaderboardEntity
{
    /// <summary>
    /// Gets or sets leaderboard rank assigned to this entity.
    /// </summary>
    /// <remarks>
    /// Read operations overwrite this value to reflect requested <see cref="Enumerations.RankingType"/>.
    /// </remarks>
    long Rank { get; set; }
}
