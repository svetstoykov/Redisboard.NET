# Redisboard.NET 🚀

### A high-performance .NET Library for creating and interacting with Leaderboards using Redis.

Redisboard.NET is a optimized .NET library designed to handle leaderboards efficiently using Redis. It provides a simple, yet powerful API to create and manage leaderboards with various ranking systems. The library leverages Redis sorted sets for performance and uses LUA scripts for advanced querying capabilities.

## 🚧 UNDER CONSTRUCTION (not available on NuGet) 🚧
**Tasks left:**
- Implement integration tests
- Run benchmarks
- Fix various issues with business logic

## Quick Start

To use Redisboard.NET, you must set up a `Leaderboard<T>` class and execute it using a resilience pipeline. The library leverages Redis sorted sets for performance and uses LUA scripts for advanced querying capabilities.

To get started, first add the [Redisboard.NET](https://www.nuget.org/packages/Redisboard.NET/) package to your project by running the following command:

```sh
dotnet add package Redisboard.NET
```

You can create and configure a Leaderboard<T> using the LeaderboardBuilder class as shown below:

<!-- snippet: quick-start -->
```cs
// Define a player class implementing ILeaderboardEntity
public class Player : ILeaderboardEntity
{
    public string Id { get; set; }
    public long Rank {get; set;}
    public double Score { get; set; }
}
```
Register the Leaderboard in your `IServiceCollection` in `Program.cs`
*Config delegate is not necessary, if you have already registered your `IConnectionMultiplexer` or `IDatabase` (ref. [StackExchange.Redis](https://github.com/StackExchange/StackExchange.Redis))

```cs
// Add to IServiceCollection
builder.Services.AddLeaderboard<Player>(cfg =>
{
    cfg.EndPoints.Add("localhost:6379");
    cfg.ClientName = "Development";
    cfg.DefaultDatabase = 0;
});
```

Add player and retrieve them from the leaderboard.
```cs
const string leaderboardKey = "your_leaderboard_key"

var players = new[]
{
    new Player { Id = "player1", Score = 100 },
    new Player { Id = "player2", Score = 150 }
};

// Add players to the leaderboard
await leaderboard.AddEntitiesAsync(leaderboardKey, players);

// Retrieve player and neighbors (offset is default 10)
var result = await leaderboard.GetEntityAndNeighboursAsync(
    leaderboardKey, "player1", RankingType.Default);
```

## Ranking
The rankings of the players inside the board are calculated on the fly via LuaScripts. Under the hood we are using multiple Redis data structures - SortedSet and HashSet. This provides us with response time of under 5ms for over 300k players in a leaderboard.

When querying data, you need to specify the `RankingType`, which can be one of the following:

| Ranking Type | Description | Example | Remarks |
| ------------- | ------------- | -------------- | ------------ |
| [**Default**](#default) | Members are ordered by score first, and if there are ties in scores, they are ordered lexicographically. There is no skipping in the records ranking. | `[{John, 100}, {Micah, 100}, {Alex, 50}, {Tim, 10}]` <br/>Ranks: `[1, 2, 3, 4]` | No skipping in ranks. |
| [**Dense Rank**](https://en.wikipedia.org/wiki/Ranking#Dense_ranking_(%221223%22_ranking)) | Items that compare equally receive the same ranking number, and the next items receive the immediately following ranking number. | Scores: `[100, 100, 50, 40, 40]` <br/>Ranks: `[1, 1, 2, 3, 3]` | Equal scores receive the same rank. Next rank is incremented by 1. |
| [**Standard Competition**](https://en.wikipedia.org/wiki/Ranking#Standard_competition_ranking_(%221224%22_ranking)) | Items that compare equally receive the same ranking number, and then a gap is left in the ranking numbers. | Scores: `[100, 80, 50, 50, 40, 30]` <br/>Ranks: `[1, 2, 3, 3, 5]` | Equal scores receive the same rank. Next rank is incremented by the number of tied members plus one. |
| [**Modified Competition**](https://en.wikipedia.org/wiki/Ranking#Modified_competition_ranking_(%221334%22_ranking)) | Leaves the gaps in the ranking numbers before the sets of equal-ranking items. | Scores: `[100, 80, 50, 50, 40, 30]` <br/>Ranks: `[1, 2, 4, 4, 5]` | Equal scores receive the same rank. Next rank is incremented by 1 regardless of the number of tied members. |

## Dependencies
- [StackExchange.Redis](https://github.com/StackExchange/StackExchange.Redis)