using Redisboard.NET.Common.Models;
using Redisboard.NET.Helpers;
using StackExchange.Redis;

namespace Redisboard.NET.Tests.Integration;

public class LeaderboardFixture : IDisposable
{
    private ConnectionMultiplexer RedisConnection { get; init; }

    private IDatabase RedisDatabase { get; init; }

    private const int TestDbInstance = 9;
    
    public LeaderboardFixture()
    {
        RedisConnection = ConnectionMultiplexer.Connect("localhost:6379");
        RedisDatabase = RedisConnection.GetDatabase(TestDbInstance);
        Instance = new Leaderboard(RedisDatabase);
        LeaderboardKey = DateTime.UtcNow.Ticks.ToString();
    }

    public string LeaderboardKey { get; init; }

    public Leaderboard Instance { get; init; }

    public void Dispose()
    {
        RedisConnection?.Dispose();
    }

    public void DeleteLeaderboardAsync()
    {
        RedisKey[] keys =
        [
            CacheKey.ForLeaderboardSortedSet(LeaderboardKey),
            CacheKey.ForEntityDataHashSet(LeaderboardKey),
            CacheKey.ForUniqueScoreSortedSet(LeaderboardKey)
        ];
        
        RedisDatabase.KeyDelete(keys);
    }
}
