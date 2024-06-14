using Redisboard.NET.DemoAPI.Helpers;
using Redisboard.NET.DemoAPI.Models;
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

await RedisHelper.SeedAsync(app, 1, 200_000);

app.MapPost("/", async (Player[] player, int leaderboardId, ILeaderboardManager<Player> leaderboardManager)
    => await leaderboardManager.AddToLeaderboardAsync(leaderboardId, player));

app.MapGet("/{id}", async (string id, int leaderboardId, ILeaderboardManager<Player> leaderboardManager)
    => await leaderboardManager.GetEntityAndNeighboursByIdAsync(leaderboardId, id));

app.Run();