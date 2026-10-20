using FluentAssertions;
using Redisboard.NET.Common.Models;
using Redisboard.NET.Enumerations;
using Redisboard.NET.Helpers;
using StackExchange.Redis;

namespace Redisboard.NET.Tests.Integration;

public class AtomicScoreTests(LeaderboardFixture fixture) : LeaderboardTestBase(fixture)
{
    [Fact]
    public async Task ConcurrentIncrements_DoNotLoseUpdates()
    {
        await SeedAsync([("player", 10)]);

        var results = await Task.WhenAll(Enumerable.Range(0, 100)
            .Select(_ => Leaderboard.IncrementEntityScoreAsync(Key, "player", 1)));

        results.Should().OnlyContain(r => r.Applied && r.Rank == 1);
        results.Select(r => r.Score).Should().OnlyHaveUniqueItems();
        (await Leaderboard.GetEntityScoreAsync(Key, "player")).Should().Be(110);
    }

    [Theory]
    [InlineData(RankingType.Default, 4)]
    [InlineData(RankingType.DenseRank, 2)]
    [InlineData(RankingType.StandardCompetition, 3)]
    [InlineData(RankingType.ModifiedCompetition, 4)]
    public async Task Decrement_ReturnsScoreAndRequestedRank(RankingType ranking, long rank)
    {
        await SeedAsync([("a", 100), ("b", 100), ("c", 50), ("player", 75)]);

        var result = await Leaderboard.DecrementEntityScoreAsync(Key, "player", 25, ranking);

        result.Applied.Should().BeTrue();
        result.Score.Should().Be(50);
        result.Rank.Should().Be(rank);
        (await Leaderboard.GetEntitiesByRankRangeAsync(Key, 1, 4, RankingType.DenseRank))
            .Select(e => e.Rank).Should().Equal(1, 1, 2, 2);
    }

    [Theory]
    [InlineData(ScoreUpdateCondition.Higher, 75, true)]
    [InlineData(ScoreUpdateCondition.Higher, 25, false)]
    [InlineData(ScoreUpdateCondition.Higher, 50, false)]
    [InlineData(ScoreUpdateCondition.Lower, 25, true)]
    [InlineData(ScoreUpdateCondition.Lower, 75, false)]
    [InlineData(ScoreUpdateCondition.Lower, 50, false)]
    [InlineData(ScoreUpdateCondition.Exists, 75, true)]
    [InlineData(ScoreUpdateCondition.NotExists, 75, false)]
    [InlineData(ScoreUpdateCondition.Always, 75, true)]
    public async Task ConditionalScoreUpdate_EvaluatesStoredScore(ScoreUpdateCondition condition, double proposed, bool applied)
    {
        await Leaderboard.AddEntityAsync(Key, new Player { Id = "player", Score = 50, Username = "original" });

        var result = await Leaderboard.UpdateEntityScoreConditionalAsync(Key, "player", proposed, condition);

        result.Applied.Should().Be(applied);
        result.Score.Should().Be(applied ? proposed : 50);
        result.Rank.Should().Be(1);
        var entity = (await Leaderboard.GetEntityAndNeighboursAsync(Key, "player")).Single();
        entity.Username.Should().Be("original");
        entity.Score.Should().Be(result.Score);
    }

    [Theory]
    [InlineData(ScoreUpdateCondition.Always, true)]
    [InlineData(ScoreUpdateCondition.NotExists, true)]
    [InlineData(ScoreUpdateCondition.Exists, false)]
    [InlineData(ScoreUpdateCondition.Higher, true)]
    [InlineData(ScoreUpdateCondition.Lower, true)]
    public async Task ConditionalAdd_HandlesMissingEntity(ScoreUpdateCondition condition, bool applied)
    {
        var player = new Player { Id = "player", Score = 50, Username = "new" };

        var result = await Leaderboard.AddEntityConditionalAsync(Key, player, condition);

        result.Applied.Should().Be(applied);
        result.Score.Should().Be(applied ? 50 : null);
        result.Rank.Should().Be(applied ? 1 : null);
        (await Leaderboard.GetSizeAsync(Key)).Should().Be(applied ? 1 : 0);
    }

    [Fact]
    public async Task ConditionalAdd_RejectedWritePreservesMetadata()
    {
        await Leaderboard.AddEntityAsync(Key, new Player { Id = "player", Score = 50, Username = "original" });

        var result = await Leaderboard.AddEntityConditionalAsync(Key,
            new Player { Id = "player", Score = 25, Username = "replacement" }, ScoreUpdateCondition.Higher);

        result.Applied.Should().BeFalse();
        (await Leaderboard.GetEntityAndNeighboursAsync(Key, "player")).Single().Username.Should().Be("original");
    }

    [Fact]
    public async Task ConcurrentPersonalBests_KeepHighestScore()
    {
        await SeedAsync([("player", 0)]);

        await Task.WhenAll(Enumerable.Range(1, 100).Select(score =>
            Leaderboard.UpdateEntityScoreConditionalAsync(Key, "player", score, ScoreUpdateCondition.Higher)));

        (await Leaderboard.GetEntityScoreAsync(Key, "player")).Should().Be(100);
        (await Leaderboard.GetEntitiesByRankRangeAsync(Key, 1, 1, RankingType.DenseRank)).Single().Rank.Should().Be(1);
    }

    [Fact]
    public async Task MissingEntity_IncrementDoesNotCreatePartialRecord()
    {
        var result = await Leaderboard.IncrementEntityScoreAsync(Key, "missing", 5);

        result.Applied.Should().BeFalse();
        result.Score.Should().BeNull();
        result.Rank.Should().BeNull();
        (await Leaderboard.GetSizeAsync(Key)).Should().Be(0);
    }

    [Theory]
    [InlineData(double.MaxValue, double.MaxValue)]
    public async Task InvalidArithmetic_PreservesScoreAndDenseRank(double initial, double delta)
    {
        await SeedAsync([("player", initial), ("other", 1)]);

        var act = () => Leaderboard.IncrementEntityScoreAsync(Key, "player", delta);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        (await Leaderboard.GetEntityScoreAsync(Key, "player")).Should().Be(initial);
        (await Leaderboard.GetEntityRankAsync(Key, "other", RankingType.DenseRank)).Should().Be(2);
    }
    [Theory]
    [InlineData(ScoreUpdateCondition.Always)]
    [InlineData(ScoreUpdateCondition.Exists)]
    [InlineData(ScoreUpdateCondition.NotExists)]
    [InlineData(ScoreUpdateCondition.Higher)]
    [InlineData(ScoreUpdateCondition.Lower)]
    public async Task MissingEntity_ConditionalScoreDoesNotCreatePartialRecord(ScoreUpdateCondition condition)
    {
        var result = await Leaderboard.UpdateEntityScoreConditionalAsync(Key, "missing", 5, condition);

        result.Applied.Should().BeFalse();
        result.Score.Should().BeNull();
        result.Rank.Should().BeNull();
        (await Leaderboard.GetSizeAsync(Key)).Should().Be(0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task IncompleteEntity_AtomicWritesPreserveLegacyRecord(bool rankingOnly)
    {
        using var connection = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
        var database = connection.GetDatabase(9);
        if (rankingOnly)
            await database.SortedSetAddAsync(CacheKey.ForLeaderboardSortedSet(Key), "player", -50);
        else
            await database.HashSetAsync(CacheKey.ForEntityDataHashSet(Key), "player", "metadata");

        var result = await Leaderboard.AddEntityConditionalAsync(Key,
            new Player { Id = "player", Score = 75 }, ScoreUpdateCondition.NotExists);

        result.Applied.Should().BeFalse();
        result.Score.Should().BeNull();
        result.Rank.Should().BeNull();
        (await database.SortedSetScoreAsync(CacheKey.ForLeaderboardSortedSet(Key), "player"))
            .Should().Be(rankingOnly ? -50 : null);
        (await database.HashGetAsync(CacheKey.ForEntityDataHashSet(Key), "player"))
            .Should().Be(rankingOnly ? RedisValue.Null : (RedisValue)"metadata");
    }

    [Fact]
    public async Task ConditionalAdd_AcceptedReplacementUpdatesMetadataAndDenseIndex()
    {
        await Leaderboard.AddEntityAsync(Key, new Player { Id = "player", Score = 50, Username = "original" });
        await SeedAsync([("other", 25)]);

        var result = await Leaderboard.AddEntityConditionalAsync(Key,
            new Player { Id = "player", Score = 100, Username = "replacement" }, ScoreUpdateCondition.Higher,
            RankingType.DenseRank);

        result.Applied.Should().BeTrue();
        result.Score.Should().Be(100);
        result.Rank.Should().Be(1);
        (await Leaderboard.GetEntityAndNeighboursAsync(Key, "player")).First().Username.Should().Be("replacement");
        (await Leaderboard.GetEntityRankAsync(Key, "other", RankingType.DenseRank)).Should().Be(2);
    }

    [Fact]
    public async Task Increment_FractionalScoreReturnsExactStoredDouble()
    {
        const double initial = 1.2345678901234567;
        const double delta = 0.0000000000000002;
        await SeedAsync([("player", initial)]);

        var result = await Leaderboard.IncrementEntityScoreAsync(Key, "player", delta);

        result.Score.Should().Be(initial + delta);
        (await Leaderboard.GetEntityScoreAsync(Key, "player")).Should().Be(result.Score);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public async Task Increment_NonfiniteDeltaRejectsBeforeWriting(double delta)
    {
        await SeedAsync([("player", 50)]);

        var act = () => Leaderboard.IncrementEntityScoreAsync(Key, "player", delta);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        (await Leaderboard.GetEntityScoreAsync(Key, "player")).Should().Be(50);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public async Task Decrement_InvalidAmountRejectsBeforeWriting(double amount)
    {
        await SeedAsync([("player", 50)]);

        var act = () => Leaderboard.DecrementEntityScoreAsync(Key, "player", amount);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        (await Leaderboard.GetEntityScoreAsync(Key, "player")).Should().Be(50);
    }

    [Fact]
    public async Task ConditionalWrite_UnknownConditionRejectsBeforeWriting()
    {
        await SeedAsync([("player", 50)]);

        var act = () => Leaderboard.UpdateEntityScoreConditionalAsync(Key, "player", 100, (ScoreUpdateCondition)99);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        (await Leaderboard.GetEntityScoreAsync(Key, "player")).Should().Be(50);
    }

    [Fact]
    public async Task Increment_CanceledTokenRejectsBeforeWriting()
    {
        await SeedAsync([("player", 50)]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var act = () => Leaderboard.IncrementEntityScoreAsync(Key, "player", 1, cancellationToken: cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        (await Leaderboard.GetEntityScoreAsync(Key, "player")).Should().Be(50);
    }

    [Fact]
    public async Task Increment_PreservesExistingUniqueScoreKeyExpiry()
    {
        await SeedAsync([("player", 50)]);
        using var connection = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
        var database = connection.GetDatabase(9);
        var uniqueScoreKey = CacheKey.ForUniqueScoreSortedSet(Key);
        await database.KeyExpireAsync(uniqueScoreKey, TimeSpan.FromMinutes(5));

        await Leaderboard.IncrementEntityScoreAsync(Key, "player", 1);

        (await database.KeyTimeToLiveAsync(uniqueScoreKey)).Should().NotBeNull();
    }

}
