namespace Redisboard.NET.Interfaces;

/// <summary>
/// Marker interface for entities stored in a leaderboard.
/// Implement this interface on your domain type and decorate exactly one property
/// with <see cref="Redisboard.NET.Attributes.LeaderboardKeyAttribute"/> and exactly one with
/// <see cref="Redisboard.NET.Attributes.LeaderboardScoreAttribute"/>.
/// The library will populate <see cref="Rank"/> automatically on every read operation.
/// </summary>
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
    /// Gets or sets the rank of the entity in the leaderboard.
    /// This value is assigned by the library on every read operation and reflects the
    /// requested <see cref="Enumerations.RankingType"/>.
    /// </summary>
    long Rank { get; set; }
}
