using Microsoft.AspNetCore.Mvc;
using Redisboard.NET.DemoAPI.Models;
using Redisboard.NET.Enumerations;
using Redisboard.NET.Interfaces;
using Redisboard.NET.IoC;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddLeaderboard<Player>(cfg =>
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

app.MapPost("/", async (Player[] player, ILeaderboard<Player> leaderboardManager)
    => await leaderboardManager.AddEntitiesAsync(1, player));

app.MapGet("/leaderboards/{leaderboardId}/players/{id}/neighbors", async (
        ILeaderboard<Player> leaderboard,
        string leaderboardId,
        string id,
        int offset = 10,
        RankingType rankingType = RankingType.Default)
    => await leaderboard.GetEntityAndNeighboursAsync(leaderboardId, id, offset, rankingType));

app.MapGet("/leaderboards/{leaderboardId}/scores", async (
        ILeaderboard<Player> leaderboardManager,
        string leaderboardId,
        double minScore,
        double maxScore,
        RankingType rankingType = RankingType.Default)
    => await leaderboardManager.GetEntitiesByScoreRangeAsync(
        leaderboardId, minScore, maxScore, rankingType));


app.Run();