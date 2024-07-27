using Redisboard.NET.Enumerations;
using StackExchange.Redis;

namespace Redisboard.NET.Interfaces;

/// <summary>
/// Represents an entity within the leaderboard.
/// </summary>
public interface ILeaderboardEntity
{
    /// <summary>
    /// Gets the unique identifier for the leaderboard entity.
    /// </summary>
    RedisValue Key { get; set; }

    /// <summary>
    /// Gets or sets the rank of the entity in the leaderboard, determined by the specified ranking type, when retrieving the records.
    /// </summary>
    /// <seealso cref="RankingType"/>
    long Rank { get; set; }

    /// <summary>
    /// Gets or sets the score associated with the entity in the leaderboard.
    /// </summary>
    double Score { get; set; }
    
    /// <summary>
    /// Gets or sets the metadata associated with the entity in the leaderboard.
    /// </summary>
    RedisValue Metadata { get; set; }
}