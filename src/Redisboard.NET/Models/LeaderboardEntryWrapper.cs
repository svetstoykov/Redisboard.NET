using Redisboard.NET.Interfaces;
using StackExchange.Redis;

namespace Redisboard.NET.Models;

/// <summary>
/// Internal concrete implementation of <see cref="ILeaderboardEntity"/> used by the
/// non-generic <see cref="Leaderboard"/> class to return query results.
/// The Key, Score and Metadata properties are concrete (not part of the interface),
/// preserved to avoid breaking the internal non-generic code path.
/// </summary>
internal class LeaderboardEntryWrapper : ILeaderboardEntity
{
    public RedisValue Key { get; set; }
    public long Rank { get; set; }
    public double Score { get; set; }
    public RedisValue Metadata { get; set; }
}