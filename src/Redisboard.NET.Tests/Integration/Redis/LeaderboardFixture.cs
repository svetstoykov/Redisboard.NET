using Redisboard.NET.Helpers;
using Redisboard.NET.Interfaces;
using Redisboard.NET.Tests.Common.Models;
using StackExchange.Redis;

namespace Redisboard.NET.Tests.Integration.Redis;

public class LeaderboardFixture : IDisposable
{
    private ConnectionMultiplexer RedisConnection { get; init; }

    private IDatabase RedisDatabase { get; init; }
    
    protected LeaderboardFixture()
    {
        RedisConnection = ConnectionMultiplexer.Connect("localhost:6379");
        RedisDatabase = RedisConnection.GetDatabase();
        Instance = new Leaderboard<TestPlayer>(RedisDatabase);
        LeaderboardId = nameof(LeaderboardFixture);
    }
    
    public ILeaderboard<TestPlayer> Instance { get; init; }

    public string LeaderboardId { get; init; }

    public void Dispose()
    {
        RedisKey[] keys =
        [
            CacheKey.ForLeaderboardSortedSet(LeaderboardId),
            CacheKey.ForEntityDataHashSet(LeaderboardId),
            CacheKey.ForUniqueScoreSortedSet(LeaderboardId)
        ];
        
        RedisDatabase.KeyDelete(keys);
        
        RedisConnection?.Dispose();
    }
}
