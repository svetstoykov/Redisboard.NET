using Redisboard.NET.Common.Models;
using Microsoft.Extensions.DependencyInjection;
using Redisboard.NET.Extensions;
using Redisboard.NET.Interfaces;
using Redisboard.NET.Enumerations;
using Redisboard.NET.Helpers;
using Redisboard.NET.Serialization;
using StackExchange.Redis;

namespace Redisboard.NET.Tests.Integration;

/// <summary>Verifies signed scores and direction across ranking operations.</summary>
public sealed class ScoreOrderTests : IDisposable
{
    private readonly ConnectionMultiplexer _connection = ConnectionMultiplexer.Connect("localhost:6379");
    private readonly string _key = $"score_order_{Guid.NewGuid():N}";

    /// <summary>Returns every score direction and ranking algorithm.</summary>
    public static IEnumerable<object[]> Modes =>
        from direction in Enum.GetValues<ScoreOrder>()
        from ranking in Enum.GetValues<RankingType>()
        select new object[] { direction, ranking };

    /// <summary>Verifies rank reads preserve signed scores and stable tie ordering.</summary>
    /// <param name="direction">Score ordering used for persistence.</param>
    /// <param name="ranking">Tie numbering algorithm.</param>
    [Theory]
    [MemberData(nameof(Modes))]
    public async Task RankReads_SignedScores_ReturnExpectedOrderAndRanks(ScoreOrder direction, RankingType ranking)
    {
        var board = Create(direction);
        await Seed(board);
        var ids = direction == ScoreOrder.Ascending
            ? new[] { "low", "10", "9", "high" } : new[] { "high", "10", "9", "low" };
        var ranks = ranking switch
        {
            RankingType.DenseRank => new long[] { 1, 2, 2, 3 },
            RankingType.StandardCompetition => new long[] { 1, 2, 2, 4 },
            RankingType.ModifiedCompetition => new long[] { 1, 3, 3, 4 },
            _ => new long[] { 1, 2, 3, 4 }
        };

        var rows = await board.GetEntitiesByRankRangeAsync(_key, 1, 4, ranking);
        var neighbors = await board.GetEntityAndNeighboursAsync(_key, "10", 10, ranking);

        Assert.Equal(ids, rows.Select(p => p.Id));
        Assert.Equal(ranks, rows.Select(p => p.Rank));
        Assert.Equal(ids, neighbors.Select(p => p.Id));
        Assert.Equal(ranks, neighbors.Select(p => p.Rank));
        Assert.Equal(direction == ScoreOrder.Ascending ? new[] { -10d, -5d, -5d, 10d }
            : new[] { 10d, -5d, -5d, -10d }, rows.Select(p => p.Score));
        for (var i = 0; i < ids.Length; i++)
            Assert.Equal(ranks[i], await board.GetEntityRankAsync(_key, ids[i], ranking));
        Assert.Equal(-10, await board.GetEntityScoreAsync(_key, "low"));
    }

    /// <summary>Verifies score ranges use user-space bounds for either direction.</summary>
    /// <param name="direction">Score ordering used for persistence.</param>
    /// <param name="ranking">Tie numbering algorithm.</param>
    [Theory]
    [MemberData(nameof(Modes))]
    public async Task ScoreRanges_SignedAndInfiniteBounds_ReturnInclusiveMatches(ScoreOrder direction, RankingType ranking)
    {
        var board = Create(direction);
        await Seed(board);

        var negative = await board.GetEntitiesByScoreRangeAsync(_key, double.NegativeInfinity, -5, ranking);
        var all = await board.GetEntitiesByScoreRangeAsync(_key, double.NegativeInfinity, double.PositiveInfinity, ranking);
        var ties = await board.GetEntitiesByScoreRangeAsync(_key, -5, -5, ranking);

        Assert.Equal(direction == ScoreOrder.Ascending ? new[] { "low", "10", "9" }
            : new[] { "10", "9", "low" }, negative.Select(p => p.Id));
        Assert.Equal(4, all.Length);
        Assert.Equal(new[] { "10", "9" }, ties.Select(p => p.Id));
        Assert.Empty(await board.GetEntitiesByScoreRangeAsync(_key, double.PositiveInfinity, double.PositiveInfinity, ranking));
        Assert.Empty(await board.GetEntitiesByScoreRangeAsync(_key, double.NegativeInfinity, double.NegativeInfinity, ranking));
    }

    /// <summary>Verifies all write paths maintain signed dense-score groups.</summary>
    /// <param name="direction">Score ordering used for persistence.</param>
    [Theory]
    [InlineData(ScoreOrder.Ascending)]
    [InlineData(ScoreOrder.Descending)]
    public async Task Mutations_CrossZero_MaintainScoresAndDenseRanks(ScoreOrder direction)
    {
        var board = Create(direction);
        await board.AddEntityAsync(_key, new Player { Id = "A", Score = -3 });
        await board.AddEntitiesAsync(_key, [new Player { Id = "B", Score = 0 }, new Player { Id = "C", Score = 4 }]);

        await board.UpdateEntityScoreAsync(_key, "A", 2);
        await board.UpdateEntityScoreAsync(_key, new Player { Id = "C", Score = -2 });
        await board.AddEntityAsync(_key, new Player { Id = "B", Score = -2 });
        await board.DeleteEntityAsync(_key, "C");
        var rows = await board.GetEntitiesByRankRangeAsync(_key, 1, 10, RankingType.DenseRank);

        Assert.Equal(direction == ScoreOrder.Ascending ? new[] { -2d, 2d } : new[] { 2d, -2d }, rows.Select(p => p.Score));
        Assert.Equal(new long[] { 1, 2 }, rows.Select(p => p.Rank));
    }

    /// <summary>Verifies the default constructor can read the existing negated storage format.</summary>
    [Fact]
    public async Task DefaultDirection_LegacyPersistedScores_RemainDescending()
    {
        var database = _connection.GetDatabase(9);
        await database.SortedSetAddAsync(CacheKey.ForLeaderboardSortedSet(_key), [new SortedSetEntry("A", -50), new SortedSetEntry("B", -10)]);
        var board = new Leaderboard<Player>(database, new MemoryPackLeaderboardSerializer());

        var rows = await board.GetEntitiesByRankRangeAsync(_key, 1, 2);

        Assert.Equal(new[] { "A", "B" }, rows.Select(p => p.Id));
        Assert.Equal(new[] { 50d, 10d }, rows.Select(p => p.Score));
    }

    /// <summary>Verifies unsupported directions fail before accessing Redis.</summary>
    [Fact]
    public void Constructor_UnsupportedDirection_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => Create((ScoreOrder)42));

    /// <summary>Verifies dependency injection forwards direction when reusing either Redis service.</summary>
    /// <param name="useMultiplexer">Whether the registration reuses a connection or a selected database.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DependencyInjection_AscendingDirection_RanksLowerScoresFirst(bool useMultiplexer)
    {
        var services = new ServiceCollection();
        if (useMultiplexer)
            services.AddSingleton<IConnectionMultiplexer>(_connection);
        else
            services.AddSingleton<IDatabase>(_connection.GetDatabase(9));
        services.AddLeaderboard<Player>(ScoreOrder.Ascending, databaseIndex: 9);
        using var provider = services.BuildServiceProvider();
        var board = provider.GetRequiredService<ILeaderboard<Player>>();
        await board.AddEntitiesAsync(_key, [new Player { Id = "A", Score = -5 }, new Player { Id = "B", Score = 5 }]);

        var rows = await board.GetEntitiesByRankRangeAsync(_key, 1, 2);

        Assert.Equal(new[] { -5d, 5d }, rows.Select(p => p.Score));
    }

    /// <summary>Verifies atomic mutations and snapshot reads share configured direction and namespace.</summary>
    /// <param name="direction">Score order used by every operation.</param>
    /// <param name="ranking">Tie numbering algorithm.</param>
    [Theory]
    [MemberData(nameof(Modes))]
    public async Task CombinedOperations_ConfiguredDirection_PreserveSignedScoresAndGlobalRanks(ScoreOrder direction, RankingType ranking)
    {
        var options = new LeaderboardOptions { KeyPrefix = $"combined:{Guid.NewGuid():N}:", UseClusterHashTags = true, ScoreOrder = direction };
        var board = new Leaderboard<Player>(_connection.GetDatabase(9), new MemoryPackLeaderboardSerializer(), options);
        try
        {
            await Seed(board);
            await board.UpdateEntityScoreAsync(_key, "10", 5);

            var incremented = await board.IncrementEntityScoreAsync(_key, "10", -10, ranking);

            var tiedRank = ranking == RankingType.ModifiedCompetition ? 3 : 2;
            Assert.True(incremented.Applied);
            Assert.Equal(-5, incremented.Score);
            Assert.Equal(tiedRank, incremented.Rank);
            Assert.Equal(-5, (await board.GetEntityAsync(_key, "10", ranking))!.Score);
            Assert.Equal(tiedRank, (await board.GetEntityAsync(_key, "10", ranking))!.Rank);
            var batch = await board.GetEntitiesByKeysAsync(_key, ["10", "missing", "low", "10"], ranking);
            Assert.Equal(new[] { -5d, -10d, -5d }, batch.Select(p => p.Score));
            var page = await board.GetPageAsync(_key, 1, 4, ranking);
            Assert.Equal(4, page.TotalCount);
            Assert.Equal(direction == ScoreOrder.Ascending ? new[] { -10d, -5d, -5d, 10d }
                : new[] { 10d, -5d, -5d, -10d }, page.Entities.Select(p => p.Score));
            var higher = await board.UpdateEntityScoreConditionalAsync(_key, "10", 1, ScoreUpdateCondition.Higher, ranking);
            Assert.True(higher.Applied);
            Assert.Equal(1, higher.Score);
            var rejected = await board.UpdateEntityScoreConditionalAsync(_key, "10", -1, ScoreUpdateCondition.Higher, ranking);
            Assert.False(rejected.Applied);
            Assert.Equal(1, rejected.Score);
            var lower = await board.AddEntityConditionalAsync(_key, new Player { Id = "10", Score = -20, Username = "changed" }, ScoreUpdateCondition.Lower, ranking);
            Assert.True(lower.Applied);
            Assert.Equal(-20, lower.Score);
            Assert.Equal(direction == ScoreOrder.Ascending ? 1 : 4, lower.Rank);
            Assert.Equal("changed", (await board.GetEntityAsync(_key, "10", ranking))!.Username);
            var decremented = await board.DecrementEntityScoreAsync(_key, "10", 2, ranking);
            Assert.Equal(-22, decremented.Score);
            Assert.Equal(direction == ScoreOrder.Ascending ? 1 : 4, decremented.Rank);
            Assert.Equal(0, await Create(direction).GetSizeAsync(_key));
        }
        finally { await board.DeleteAsync(_key); }
    }

    /// <summary>Verifies constructors and DI snapshot score direction alongside key configuration.</summary>
    /// <param name="useDi">Whether to resolve through dependency injection.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Options_DirectionAndKeySettings_AreCopied(bool useDi)
    {
        var options = new LeaderboardOptions { KeyPrefix = $"options:{Guid.NewGuid():N}:", UseClusterHashTags = true, ScoreOrder = ScoreOrder.Ascending };
        var services = new ServiceCollection();
        services.AddSingleton<IConnectionMultiplexer>(_connection);
        services.AddLeaderboard<Player>(optionsAction: null, leaderboardOptions: options, databaseIndex: 9);
        var constructed = new Leaderboard<Player>(_connection, new MemoryPackLeaderboardSerializer(), options, 9);
        options.ScoreOrder = ScoreOrder.Descending;
        options.KeyPrefix = "different:";
        options.UseClusterHashTags = false;
        using var provider = services.BuildServiceProvider();
        var board = useDi ? provider.GetRequiredService<ILeaderboard<Player>>() : constructed;
        try
        {
            await board.AddEntitiesAsync(_key, [new Player { Id = "A", Score = -1 }, new Player { Id = "B", Score = 1 }]);

            var rows = await constructed.GetPageAsync(_key, 1, 10);

            Assert.Equal(new[] { -1d, 1d }, rows.Entities.Select(p => p.Score));
            Assert.Equal(0, await Create(ScoreOrder.Descending).GetSizeAsync(_key));
        }
        finally { await board.DeleteAsync(_key); }
    }

    /// <summary>Verifies unsupported option directions fail at construction and DI registration.</summary>
    [Fact]
    public void Options_UnsupportedDirection_RejectsBeforeRedis()
    {
        var options = new LeaderboardOptions { ScoreOrder = (ScoreOrder)42 };

        Assert.Throws<ArgumentOutOfRangeException>(() => new Leaderboard<Player>(_connection.GetDatabase(9), new MemoryPackLeaderboardSerializer(), options));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ServiceCollection().AddLeaderboard<Player>(optionsAction: null, leaderboardOptions: options));
    }

    /// <summary>Verifies signed mutations, snapshot reads, and expiry against an actual Redis Cluster.</summary>
    /// <param name="direction">Score order used by the clustered board.</param>
    [ClusterTheory]
    [InlineData(ScoreOrder.Ascending)]
    [InlineData(ScoreOrder.Descending)]
    public async Task Cluster_AllFeatures_ShareKeysDirectionAndExpiry(ScoreOrder direction)
    {
        using var connection = await ConnectionMultiplexer.ConnectAsync(Environment.GetEnvironmentVariable("REDIS_CLUSTER_CONNECTION")!);
        var options = new LeaderboardOptions { KeyPrefix = $"rb010:{Guid.NewGuid():N}:", UseClusterHashTags = true, ScoreOrder = direction };
        var board = new Leaderboard<Player>(connection, new MemoryPackLeaderboardSerializer(), options);
        var database = connection.GetDatabase();
        var keys = new[] { CacheKey.ForLeaderboardSortedSet(_key, options), CacheKey.ForUniqueScoreSortedSet(_key, options), CacheKey.ForEntityDataHashSet(_key, options) };
        try
        {
            await board.AddEntityConditionalAsync(_key, new Player { Id = "A", Score = -5 }, ScoreUpdateCondition.NotExists);
            await board.ExpireAsync(_key, TimeSpan.FromMinutes(5));
            foreach (var ranking in Enum.GetValues<RankingType>())
            {
                Assert.Equal(-4, (await board.IncrementEntityScoreAsync(_key, "A", 1, ranking)).Score);
                Assert.Equal(-6, (await board.UpdateEntityScoreConditionalAsync(_key, "A", -6, ScoreUpdateCondition.Lower, ranking)).Score);
                Assert.Equal(-5, (await board.AddEntityConditionalAsync(_key, new Player { Id = "A", Score = -5 }, ScoreUpdateCondition.Higher, ranking)).Score);
                Assert.Equal(-5, (await board.GetEntityAsync(_key, "A", ranking))!.Score);
                Assert.Equal(-5, Assert.Single(await board.GetEntitiesByKeysAsync(_key, ["A"], ranking)).Score);
                var page = await board.GetPageAsync(_key, 1, 10, ranking);
                Assert.Equal(1, page.TotalCount);
                Assert.Equal(-5, Assert.Single(page.Entities).Score);
            }
            var deadlines = await Task.WhenAll(keys.Select(k => database.KeyExpireTimeAsync(k)));
            Assert.All(deadlines, deadline => Assert.NotNull(deadline));
            Assert.All(deadlines, deadline => Assert.Equal(deadlines[0], deadline));
            await board.ExpireAsync(_key, TimeSpan.Zero);
            Assert.Equal(0, (await board.GetPageAsync(_key, 1, 10)).TotalCount);
            foreach (var key in keys) Assert.False(await database.KeyExistsAsync(key));
        }
        finally { await board.DeleteAsync(_key); }
    }

    /// <summary>Verifies signed arithmetic overflow leaves scores and ranking indexes unchanged.</summary>
    /// <param name="direction">Score encoding used for the board.</param>
    [Theory]
    [InlineData(ScoreOrder.Ascending)]
    [InlineData(ScoreOrder.Descending)]
    public async Task Increment_NegativeOverflow_DoesNotWrite(ScoreOrder direction)
    {
        var board = Create(direction);
        await board.AddEntityAsync(_key, new Player { Id = "A", Score = -double.MaxValue });

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => board.IncrementEntityScoreAsync(_key, "A", -double.MaxValue));

        Assert.Equal(-double.MaxValue, await board.GetEntityScoreAsync(_key, "A"));
        Assert.Equal(1, await board.GetEntityRankAsync(_key, "A", RankingType.DenseRank));
    }

    /// <summary>Returns score direction and exact finite double boundaries.</summary>
    public static IEnumerable<object[]> NumericBoundaries =>
        from direction in Enum.GetValues<ScoreOrder>()
        from score in new[] { double.Epsilon, -double.Epsilon, double.MaxValue, -double.MaxValue }
        select new object[] { direction, score };

    /// <summary>Verifies atomic writes round-trip subnormal and extreme signed doubles exactly.</summary>
    /// <param name="direction">Score encoding used for persistence.</param>
    /// <param name="score">Exact finite boundary value.</param>
    [Theory]
    [MemberData(nameof(NumericBoundaries))]
    public async Task AtomicWrites_FiniteDoubleBoundaries_PreserveExactScoresAndDenseGroups(ScoreOrder direction, double score)
    {
        var board = Create(direction);
        await board.AddEntityAsync(_key, new Player { Id = "zero", Score = 0 });

        var inserted = await board.AddEntityConditionalAsync(_key, new Player { Id = "A", Score = score }, ScoreUpdateCondition.NotExists, RankingType.DenseRank);
        var unchanged = await board.IncrementEntityScoreAsync(_key, "A", 0, RankingType.DenseRank);
        var updated = await board.UpdateEntityScoreConditionalAsync(_key, "A", -score, ScoreUpdateCondition.Always, RankingType.DenseRank);

        var expectedRank = (score < 0) == (direction == ScoreOrder.Ascending) ? 1 : 2;
        Assert.Equal(score, inserted.Score);
        Assert.Equal(expectedRank, inserted.Rank);
        Assert.Equal(score, unchanged.Score);
        Assert.Equal(expectedRank, unchanged.Rank);
        Assert.Equal(-score, updated.Score);
        Assert.Equal(3 - expectedRank, updated.Rank);
        Assert.Equal(-score, await board.GetEntityScoreAsync(_key, "A"));
        var rows = await board.GetPageAsync(_key, 1, 10, RankingType.DenseRank);
        Assert.Equal(new long[] { 1, 2 }, rows.Entities.Select(p => p.Rank));
        Assert.Contains(rows.Entities, entity => entity.Id == "A" && entity.Score == -score);
    }

    private sealed class ClusterTheoryAttribute : TheoryAttribute
    {
        public ClusterTheoryAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("REDIS_CLUSTER_CONNECTION")))
                Skip = "Set REDIS_CLUSTER_CONNECTION to run against Redis Cluster.";
        }
    }

    private Leaderboard<Player> Create(ScoreOrder direction)
        => new(_connection, new MemoryPackLeaderboardSerializer(), direction, 9);

    private Task Seed(Leaderboard<Player> board)
        => board.AddEntitiesAsync(_key, [new Player { Id = "low", Score = -10 },
            new Player { Id = "9", Score = -5 }, new Player { Id = "10", Score = -5 },
            new Player { Id = "high", Score = 10 }]);

    /// <summary>Removes this test's Redis data and disposes its connection.</summary>
    public void Dispose()
    {
        Create(ScoreOrder.Descending).DeleteAsync(_key).GetAwaiter().GetResult();
        _connection.Dispose();
    }
}
