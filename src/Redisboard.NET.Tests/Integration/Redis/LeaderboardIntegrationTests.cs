namespace Redisboard.NET.Tests.Integration.Redis;

public class LeaderboardIntegrationTests : IClassFixture<LeaderboardFixture>
{
    private readonly LeaderboardFixture _leaderboardFixture;

    public LeaderboardIntegrationTests(LeaderboardFixture leaderboardFixture)
    {
        _leaderboardFixture = leaderboardFixture;
    }

    [Fact]
    public async Task GetEntityAndNeighboursAsync_WithValidDataDefaultRanking_ReturnsLeaderboard()
    {
        
    }
}