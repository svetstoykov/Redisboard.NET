# Redisboard.NET 🚀

### A high-performance .NET Library for creating and interacting with Leaderboards using Redis.

Redisboard.NET is a optimized .NET library designed to handle leaderboards efficiently using Redis. It provides a simple, yet powerful API to create and manage leaderboards with various ranking systems. The library leverages Redis sorted sets for performance and uses LUA scripts for advanced querying capabilities.

## 🚧 UNDER CONSTRUCTION (not available on NuGet) 🚧

**TODO:**  
✅ Implement integration tests   
❌ Add benchmarks  
❌ Distribute to NuGet 

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
The rankings of the players are calculated on the fly when querying the data. Under the hood we are using multiple Redis data structures (SortedSet and HashSet) and LUA scripts. This provides us with a response time of under 5ms for over 300k players in a leaderboard.

When querying data, you need to specify the `RankingType`, which can be one of the following:


### 1. Default Ranking 🏆
Members are ordered by score first, and if there are ties in scores, they are then ordered **lexicographically**. There is no skipping in the records ranking. *This is Redis Sorted Set default ranking style.*

**Example:**
```
Scores: [{John, 100}, {Micah, 100}, {Alex, 99}, {Tim, 1}]
Ranks: [1, 2, 3, 4]
```


### 2. [Dense Rank](https://en.wikipedia.org/wiki/Ranking#Dense_ranking_(%221223%22_ranking)) 🥇🥈
Items that compare equally receive the same ranking number, and the next items receive the immediately following ranking number.

**Example:**
```
Scores: [100, 80, 50, 50, 40, 10]
Ranks: [1, 2, 3, 3, 4, 5]
```


### 3. [Standard Competition](https://en.wikipedia.org/wiki/Ranking#Standard_competition_ranking_(%221224%22_ranking)) 🏅
Items that compare equally receive the same ranking number, and then a gap is left in the ranking numbers.

**Example:**
```
Scores: [100, 80, 50, 50, 40, 10]
Ranks: [1, 2, 3, 3, 5, 6]
```


### 4. [Modified Competition](https://en.wikipedia.org/wiki/Ranking#Modified_competition_ranking_(%221334%22_ranking)) 🎖️
Leaves the gaps in the ranking numbers before the sets of equal-ranking items.

**Example:**
```
Scores: [100, 80, 50, 50, 40, 10]
Ranks: [1, 2, 4, 4, 5, 6]
```

## Dependencies
- [StackExchange.Redis](https://github.com/StackExchange/StackExchange.Redis)