using Microsoft.AspNetCore.Mvc;
using Redisboard.NET.DemoAPI.Models;
using Redisboard.NET.Enumerations;
using Redisboard.NET.Interfaces;
using Redisboard.NET.IoC;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddLeaderboardManager<Player>(cfg =>
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

// await RedisHelper.SeedAsync(app, 1, 200_000);

app.MapPost("/", async (Player[] player, ILeaderboard<Player> leaderboardManager)
    => await leaderboardManager.AddEntitiesAsync(1, player));

app.MapGet("/leaderboards/{leaderboardId}/players/{id}/neighbors", async (
        ILeaderboard<Player> leaderboard,
        string leaderboardId,
        string id,
        int offset,
        RankingType rankingType = RankingType.Default,
        CancellationToken cancellationToken = default)
    => await leaderboard.GetEntityAndNeighboursAsync(leaderboardId, id, offset, rankingType));

app.MapGet("/leaderboards/{leaderboardId}/scores", async (
        ILeaderboard<Player> leaderboardManager,
        string leaderboardId,
        double minScore,
        double maxScore,
        RankingType rankingType = RankingType.Default,
        CancellationToken cancellationToken = default)
    => await leaderboardManager.GetEntitiesByScoreRangeAsync(
        leaderboardId, minScore, maxScore, rankingType));


app.Run();