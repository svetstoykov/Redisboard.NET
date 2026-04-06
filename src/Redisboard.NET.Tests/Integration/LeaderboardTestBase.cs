using Redisboard.NET.Common.Models;

namespace Redisboard.NET.Tests.Integration;

/// <summary>
/// Base class for leaderboard integration tests. Provides a unique key per
/// test-class instance so tests are fully isolated, and a bounded-concurrency
/// seed helper for populating data.
/// </summary>
public abstract class LeaderboardTestBase : IClassFixture<LeaderboardFixture>, IDisposable
{
    protected readonly Leaderboard<Player> Leaderboard;

    /// <summary>Unique key per test-class instance — ensures full test isolation.</summary>
    protected readonly string Key = $"test_{DateTime.UtcNow.Ticks}_{Guid.NewGuid():N}";

    protected LeaderboardTestBase(LeaderboardFixture fixture)
    {
        Leaderboard = fixture.Instance;
    }

    /// <summary>
    /// Seeds players with the given (key, score) pairs using bounded concurrency.
    /// Creates <see cref="Player"/> instances with <see cref="Player.Id"/> = key and
    /// <see cref="Player.Score"/> = score.
    /// </summary>
    protected async Task SeedAsync(IEnumerable<(string key, double score)> players, int concurrency = 50)
    {
        var sem = new SemaphoreSlim(concurrency);
        var tasks = players.Select(async p =>
        {
            await sem.WaitAsync();
            try
            {
                var player = new Player { Id = p.key, Score = p.score };
                await Leaderboard.AddEntityAsync(Key, player);
            }
            finally
            {
                sem.Release();
            }
        });
        await Task.WhenAll(tasks);
    }

    public void Dispose()
    {
        Leaderboard.DeleteAsync(Key).GetAwaiter().GetResult();
    }
}
