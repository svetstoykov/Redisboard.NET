namespace Redisboard.NET.Tests.Integration.Redis;

public class LeaderboardIntegrationTests : IClassFixture<LeaderboardFixture>
{
    private readonly LeaderboardFixture _leaderboardFixture;

    public LeaderboardIntegrationTests(LeaderboardFixture leaderboardFixture)
    {
        _leaderboardFixture = leaderboardFixture;
    }
}