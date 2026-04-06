using System.Text.Json;
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

// Seed endpoint
app.MapPost("/seed", async (SeedLeaderboardRequest player, ILeaderboard leaderboard) =>
    await LeaderboardSeeder.SeedAsync(leaderboard, player.LeaderboardKey, player.NumberOfEntities));

// Add entity
app.MapPost("/leaderboards/{leaderboardId}/players", async (
        ILeaderboard leaderboard,
        string leaderboardId,
        AddPlayerRequest request) =>
{
    await leaderboard.AddEntityAsync(leaderboardId, request.PlayerId, request.Metadata);
    return Results.Ok(new { Message = "Entity added successfully" });
});

// Get neighbors
app.MapGet("/leaderboards/{leaderboardId}/players/{id}/neighbors", async (
        ILeaderboard leaderboard,
        string leaderboardId,
        string id,
        int offset = 10,
        RankingType rankingType = RankingType.Default) =>
{
    var entities = await leaderboard.GetEntityAndNeighboursAsync(leaderboardId, id, offset, rankingType);
    return entities.Select(PlayerResponse.MapFromLeaderboardEntity);
});

// Get by score range
app.MapGet("/leaderboards/{leaderboardId}/scores", async (
        ILeaderboard leaderboard,
        string leaderboardId,
        double minScore,
        double maxScore,
        RankingType rankingType = RankingType.Default) =>
{
    var entities = await leaderboard.GetEntitiesByScoreRangeAsync(leaderboardId, minScore, maxScore, rankingType);
    return entities.Select(PlayerResponse.MapFromLeaderboardEntity);
});

// Update score
app.MapPut("/leaderboards/{leaderboardId}/players/{id}/score", async (
        ILeaderboard leaderboard,
        string leaderboardId,
        string id,
        UpdateScoreRequest request) =>
{
    await leaderboard.UpdateEntityScoreAsync(leaderboardId, id, request.NewScore);
    return Results.Ok(new { Message = "Score updated successfully" });
});

// Update metadata
app.MapPut("/leaderboards/{leaderboardId}/players/{id}/metadata", async (
        ILeaderboard leaderboard,
        string leaderboardId,
        string id,
        UpdateMetadataRequest request) =>
{
    await leaderboard.UpdateEntityMetadataAsync(leaderboardId, id, request.Metadata);
    return Results.Ok(new { Message = "Metadata updated successfully" });
});

// Get score
app.MapGet("/leaderboards/{leaderboardId}/players/{id}/score", async (
        ILeaderboard leaderboard,
        string leaderboardId,
        string id) =>
{
    var score = await leaderboard.GetEntityScoreAsync(leaderboardId, id);
    return new EntityResponse { Key = id, Score = score };
});

// Get rank
app.MapGet("/leaderboards/{leaderboardId}/players/{id}/rank", async (
        ILeaderboard leaderboard,
        string leaderboardId,
        string id,
        RankingType rankingType = RankingType.Default) =>
{
    var rank = await leaderboard.GetEntityRankAsync(leaderboardId, id, rankingType);
    return new EntityResponse { Key = id, Rank = rank };
});

// Get metadata
app.MapGet("/leaderboards/{leaderboardId}/players/{id}/metadata", async (
        ILeaderboard leaderboard,
        string leaderboardId,
        string id) =>
{
    var metadata = await leaderboard.GetEntityMetadataAsync(leaderboardId, id);
    return new EntityResponse { Key = id, Metadata = metadata };
});

// Get by rank range
app.MapGet("/leaderboards/{leaderboardId}/players", async (
        ILeaderboard leaderboard,
        string leaderboardId,
        long startRank,
        long endRank,
        RankingType rankingType = RankingType.Default) =>
{
    var entities = await leaderboard.GetEntitiesByRankRangeAsync(leaderboardId, startRank, endRank, rankingType);
    return entities.Select(PlayerResponse.MapFromLeaderboardEntity);
});

// Get size
app.MapGet("/leaderboards/{leaderboardId}/size", async (
        ILeaderboard leaderboard,
        string leaderboardId) =>
{
    var size = await leaderboard.GetSizeAsync(leaderboardId);
    return new SizeResponse { Size = size };
});

// Delete entity
app.MapDelete("/leaderboards/{leaderboardId}/players/{id}", async (
        ILeaderboard leaderboard,
        string leaderboardId,
        string id) =>
{
    await leaderboard.DeleteEntityAsync(leaderboardId, id);
    return Results.Ok(new { Message = "Entity deleted successfully" });
});

// Delete leaderboard
app.MapDelete("/leaderboards/{leaderboardId}", async (
        ILeaderboard leaderboard,
        string leaderboardId) =>
{
    await leaderboard.DeleteAsync(leaderboardId);
    return Results.Ok(new { Message = "Leaderboard deleted successfully" });
});

app.Run();