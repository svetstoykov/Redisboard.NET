using Redisboard.NET.Enumerations;

namespace Redisboard.NET.Interfaces;

/// <summary>
/// Interface for leaderboard entities to be stored in Redis.
/// Implement this interface for any object you wish to store in the hash set,
/// ensuring it can be identified by a unique ID used both in the hash set and the sorted set.
/// </summary>
public interface ILeaderboardEntity
{
    /// <summary>
    /// Gets the unique identifier for the leaderboard entity.
    /// This ID is used as the key in both the hash set and the sorted set.
    /// </summary>
    string Id { get; set; }
    
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