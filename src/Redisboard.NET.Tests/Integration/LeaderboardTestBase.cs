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

    /// <summary>
    /// Seeds players in Redis using batch add operations.
    /// This is optimized for large datasets while respecting the API batch limit.
    /// </summary>
    protected async Task SeedBulkAsync(IEnumerable<(string key, double score)> players, int batchSize = 1_000)
    {
        const int maxBatchSize = 10_000;

        if (batchSize <= 0 || batchSize > maxBatchSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(batchSize),
                $"Batch size must be between 1 and {maxBatchSize}.");
        }

        var entries = players
            .Select(p => new Player { Id = p.key, Score = p.score })
            .ToArray();

        foreach (var batch in entries.Chunk(batchSize))
        {
            await Leaderboard.AddEntitiesAsync(Key, batch);
        }
    }

    protected static async Task<T> RoundTripAsync<T>(Func<Task> mutate, Func<Task<T>> observe)
    {
        await mutate();
        return await observe();
    }

    public void Dispose()
    {
        Leaderboard.DeleteAsync(Key).GetAwaiter().GetResult();
    }
}
