namespace Redisboard.NET.Enumerations;

/// <summary>Defines which score ranks first in a leaderboard.</summary>
/// <remarks>
/// Direction controls score encoding at rest. All clients accessing a leaderboard must use the same direction.
/// Equal scores always use ascending Redis member-byte ordering, independently of <see cref="RankingType"/>.
/// </remarks>
public enum ScoreOrder
{
    /// <summary>Ranks higher scores first, preserving the default persisted format.</summary>
    Descending = 0,

    /// <summary>Ranks lower scores first.</summary>
    Ascending = 1
}
