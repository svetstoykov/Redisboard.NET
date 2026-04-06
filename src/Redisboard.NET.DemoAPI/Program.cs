using Redisboard.NET.Common.Helpers;
using Redisboard.NET.Common.Models;
using Redisboard.NET.DemoAPI.Models;
using Redisboard.NET.Enumerations;
using Redisboard.NET.Extensions;
using Redisboard.NET.Interfaces;

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

// Seed endpoint
app.MapPost("/seed", async (SeedLeaderboardRequest request, ILeaderboard<Player> leaderboard) =>
    await LeaderboardSeeder.SeedAsync(leaderboard, request.LeaderboardKey, request.NumberOfEntities));

// Add player
app.MapPost("/leaderboards/{leaderboardId}/players", async (
        ILeaderboard<Player> leaderboard,
        string leaderboardId,
        AddPlayerRequest request) =>
{
    var player = new Player
    {
        Id = request.Id,
        Score = request.Score,
        Username = request.Username,
        FirstName = request.FirstName,
        LastName = request.LastName,
        EntryDate = DateTime.UtcNow
    };

    await leaderboard.AddEntityAsync(leaderboardId, player);
    return Results.Ok(new { Message = "Player added successfully" });
});

// Get neighbours
app.MapGet("/leaderboards/{leaderboardId}/players/{id}/neighbors", async (
        ILeaderboard<Player> leaderboard,
        string leaderboardId,
        string id,
        int offset = 10,
        RankingType rankingType = RankingType.Default) =>
{
    var players = await leaderboard.GetEntityAndNeighboursAsync(leaderboardId, id, offset, rankingType);
    return players.Select(PlayerResponse.From);
});

// Get by score range
app.MapGet("/leaderboards/{leaderboardId}/scores", async (
        ILeaderboard<Player> leaderboard,
        string leaderboardId,
        double minScore,
        double maxScore,
        RankingType rankingType = RankingType.Default) =>
{
    var players = await leaderboard.GetEntitiesByScoreRangeAsync(leaderboardId, minScore, maxScore, rankingType);
    return players.Select(PlayerResponse.From);
});

// Update score
app.MapPut("/leaderboards/{leaderboardId}/players/{id}/score", async (
        ILeaderboard<Player> leaderboard,
        string leaderboardId,
        string id,
        UpdateScoreRequest request) =>
{
    // Read current state then update score
    var existing = await leaderboard.GetEntityAndNeighboursAsync(leaderboardId, id, offset: 0);
    if (existing.Length == 0) return Results.NotFound();

    var player = existing.First(p => p.Id == id);
    player.Score = request.NewScore;

    await leaderboard.UpdateEntityScoreAsync(leaderboardId, player);
    return Results.Ok(new { Message = "Score updated successfully" });
});

// Get score
app.MapGet("/leaderboards/{leaderboardId}/players/{id}/score", async (
        ILeaderboard<Player> leaderboard,
        string leaderboardId,
        string id) =>
{
    var score = await leaderboard.GetEntityScoreAsync(leaderboardId, id);
    return new EntityResponse { Key = id, Score = score };
});

// Get rank
app.MapGet("/leaderboards/{leaderboardId}/players/{id}/rank", async (
        ILeaderboard<Player> leaderboard,
        string leaderboardId,
        string id,
        RankingType rankingType = RankingType.Default) =>
{
    var rank = await leaderboard.GetEntityRankAsync(leaderboardId, id, rankingType);
    return new EntityResponse { Key = id, Rank = rank };
});

// Get by rank range
app.MapGet("/leaderboards/{leaderboardId}/players", async (
        ILeaderboard<Player> leaderboard,
        string leaderboardId,
        long startRank,
        long endRank,
        RankingType rankingType = RankingType.Default) =>
{
    var players = await leaderboard.GetEntitiesByRankRangeAsync(leaderboardId, startRank, endRank, rankingType);
    return players.Select(PlayerResponse.From);
});

// Get size
app.MapGet("/leaderboards/{leaderboardId}/size", async (
        ILeaderboard<Player> leaderboard,
        string leaderboardId) =>
{
    var size = await leaderboard.GetSizeAsync(leaderboardId);
    return new SizeResponse { Size = size };
});

// Delete player
app.MapDelete("/leaderboards/{leaderboardId}/players/{id}", async (
        ILeaderboard<Player> leaderboard,
        string leaderboardId,
        string id) =>
{
    await leaderboard.DeleteEntityAsync(leaderboardId, id);
    return Results.Ok(new { Message = "Player deleted successfully" });
});

// Delete leaderboard
app.MapDelete("/leaderboards/{leaderboardId}", async (
        ILeaderboard<Player> leaderboard,
        string leaderboardId) =>
{
    await leaderboard.DeleteAsync(leaderboardId);
    return Results.Ok(new { Message = "Leaderboard deleted successfully" });
});

app.Run();
