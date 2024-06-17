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

app.MapPost("/", async (Player[] player, ILeaderboardManager<Player> leaderboardManager)
    => await leaderboardManager.AddEnitityToLeaderboardAsync(1, player));

app.MapGet("/leaderboards/{leaderboardId}/players/{id}/neighbors", async (
        ILeaderboardManager<Player> leaderboardManager,
        string leaderboardId, 
        string id, 
        int offset,
        RankingType rankingType = RankingType.Default,
        CancellationToken cancellationToken = default) 
    => await leaderboardManager.GetEntityAndNeighboursByIdAsync(
        leaderboardId, id, offset, rankingType, cancellationToken));


app.Run();