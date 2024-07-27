using Redisboard.NET.Interfaces;
using StackExchange.Redis;

namespace Redisboard.NET.Models;

internal class LeaderboardEntryWrapper : ILeaderboardEntity
{
    public RedisValue Key { get; set; }
    public long Rank { get; set; }
    public double Score { get; set; }
    public RedisValue Metadata { get; set; }
}