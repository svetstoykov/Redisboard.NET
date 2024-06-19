using Redisboard.NET.Enumerations;

namespace Redisboard.NET.Interfaces;

/// <summary>
/// Represents an entity within the leaderboard.
/// </summary>
public interface ILeaderboardEntity
{
    /// <summary>
    /// Gets the unique identifier for the leaderboard entity.
    /// </summary>
    string Key { get; }
    
    /// <summary>
    /// Gets or sets the rank of the entity in the leaderboard, determined by the specified ranking type, when retrieving the records.
    /// </summary>
    /// <seealso cref="RankingType"/>
    long Rank { get; set; }
    
    /// <summary>
    /// Gets or sets the score associated with the entity in the leaderboard.
    /// </summary>
    double Score { get; set; }
}