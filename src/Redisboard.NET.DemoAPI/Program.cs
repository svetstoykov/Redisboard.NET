using Redisboard.NET.Common.Helpers;
using Redisboard.NET.Common.Models;
using Redisboard.NET.DemoAPI.Models;
using Redisboard.NET.Enumerations;
using Redisboard.NET.Extensions;
using Redisboard.NET.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddLeaderboard(cfg =>
{
    cfg.EndPoints.Add("localhost:6379");
    cfg.ClientName = "Development";
    cfg.DefaultDatabase = 0;
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapPost("/seed", async (SeedLeaderboardRequest player, ILeaderboard leaderboard)
    => await LeaderboardSeeder.SeedAsync(leaderboard, player.LeaderboardKey, player.NumberOfEntities));

app.MapGet("/leaderboards/{leaderboardId}/players/{id}/neighbors", async (
        ILeaderboard leaderboard, string leaderboardId, string id, int offset = 10, RankingType rankingType = RankingType.Default)
    => (await leaderboard.GetEntityAndNeighboursAsync(leaderboardId, id, offset, rankingType))
    .Select(PlayerResponse.MapFromLeaderboardEntity));

app.MapGet("/leaderboards/{leaderboardId}/scores", async (
        ILeaderboard leaderboardManager, string leaderboardId, double minScore, double maxScore, RankingType rankingType = RankingType.Default)
    => (await leaderboardManager.GetEntitiesByScoreRangeAsync(
        leaderboardId, minScore, maxScore, rankingType))
    .Select(PlayerResponse.MapFromLeaderboardEntity));

app.Run();