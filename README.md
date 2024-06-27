# Redisboard.NET 🚀

### A high-performance .NET Library for creating and interacting with Leaderboards using Redis.

Redisboard.NET is a optimized .NET library designed to handle leaderboards efficiently using Redis. It provides a simple, yet powerful API to create and manage leaderboards with various ranking systems. The library leverages Redis sorted sets for performance and uses LUA scripts for advanced querying capabilities.

## 🚧 UNDER CONSTRUCTION (not available on NuGet) 🚧

**TODO:**  
✅ Add integration tests   
✅ Add benchmarks  
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

Start managing your leaderboard with the help of the `Leaderboard<Player>` class
```cs
const string leaderboardKey = "your_leaderboard_key"

//initialize a new Leaderboard class by passing down IDatabase or IConnectionMultiplexer
var leaderboard = new Leaderboard<Player>(redis) 

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
If you wish to use **Dependency Injection** you can install the [Redisboard.NET.Extensions]() package and register the Leaderboard in your `IServiceCollection`  

```cs
// Add to IServiceCollection
builder.Services.AddLeaderboard<Player>(cfg =>
{
    cfg.EndPoints.Add("localhost:6379");
    cfg.ClientName = "Development";
    cfg.DefaultDatabase = 0;
});
```

*\* Config delegate is not required, if you have already registered your `IConnectionMultiplexer` or `IDatabase` (ref. [StackExchange.Redis](https://github.com/StackExchange/StackExchange.Redis))*

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

## Benchmarks 🚀

These benchmarks have been run over a leaderboard with **500,000 entries** of type:
```cs
public class Player : ILeaderboardEntity
{
    public string Key { get; set; }
    public long Rank { get; set; }
    public double Score { get; set; }
    public string Username { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public DateTime EntryDate { get; set; }
}
```

We are testing the most common method - getting a user with their neighbors.


<table>
  <thead>
    <tr>
      <th>Method</th>
      <th>Offset</th>
      <th>Mean</th>
      <th>Error</th>
      <th>StdDev</th>
      <th>Median</th>
      <th>Min</th>
      <th>Max</th>
      <th>Ratio</th>
      <th>Allocated</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td>GetEntityAndNeighbours_DefaultRanking</td>
      <td>10</td>
      <td>0.329 ms</td>
      <td>0.010 ms</td>
      <td>0.032 ms</td>
      <td>0.328 ms</td>
      <td>0.257 ms</td>
      <td>0.414 ms</td>
      <td>1.00</td>
      <td>31.49 KB</td>
    </tr>
    <tr >
      <td>GetEntityAndNeighbours_DenseRanking</td>
      <td>10</td>
      <td>0.341 ms</td>
      <td>0.009 ms</td>
      <td>0.027 ms</td>
      <td>0.332 ms</td>
      <td>0.280 ms</td>
      <td>0.409 ms</td>
      <td>1.05</td>
      <td>43 KB</td>
    </tr>
    <tr>
      <td>GetEntityAndNeighbours_Competition</td>
      <td>10</td>
      <td>0.371 ms</td>
      <td>0.007 ms</td>
      <td>0.017 ms</td>
      <td>0.368 ms</td>
      <td>0.318 ms</td>
      <td>0.405 ms</td>
      <td>1.13</td>
      <td>46.97 KB</td>
    </tr>
    <tr style="background-color:rgba(51, 170, 51, .4)">
      <td>GetEntityAndNeighbours_DefaultRanking</td>
      <td>20</td>
      <td>0.410 ms</td>
      <td>0.017 ms</td>
      <td>0.050 ms</td>
      <td>0.411 ms</td>
      <td>0.305 ms</td>
      <td>0.511 ms</td>
      <td>1.00</td>
      <td>59.84 KB</td>
    </tr>
    <tr style="background-color:rgba(51, 170, 51, .4)">
      <td>GetEntityAndNeighbours_DenseRanking</td>
      <td>20</td>
      <td>0.509 ms</td>
      <td>0.010 ms</td>
      <td>0.018 ms</td>
      <td>0.515 ms</td>
      <td>0.456 ms</td>
      <td>0.540 ms</td>
      <td>1.40</td>
      <td>75.99 KB</td>
    </tr>
    <tr style="background-color:rgba(51, 170, 51, .4)">
      <td>GetEntityAndNeighbours_Competition</td>
      <td>20</td>
      <td>0.544 ms</td>
      <td>0.008 ms</td>
      <td>0.007 ms</td>
      <td>0.543 ms</td>
      <td>0.531 ms</td>
      <td>0.558 ms</td>
      <td>1.50</td>
      <td>80.09 KB</td>
    </tr>
    <tr style="background-color:rgba(90, 200, 240, .4">
      <td>GetEntityAndNeighbours_DefaultRanking</td>
      <td>50</td>
      <td>0.503 ms</td>
      <td>0.006 ms</td>
      <td>0.005 ms</td>
      <td>0.505 ms</td>
      <td>0.494 ms</td>
      <td>0.514 ms</td>
      <td>1.00</td>
      <td>142.16 KB</td>
    </tr>
    <tr style="background-color:rgba(90, 200, 240, .4">
      <td>GetEntityAndNeighbours_DenseRanking</td>
      <td>50</td>
      <td>0.620 ms</td>
      <td>0.010 ms</td>
      <td>0.013 ms</td>
      <td>0.618 ms</td>
      <td>0.593 ms</td>
      <td>0.645 ms</td>
      <td>1.24</td>
      <td>171.78 KB</td>
    </tr>
    <tr style="background-color:rgba(90, 200, 240, .4">
      <td>GetEntityAndNeighbours_Competition</td>
      <td>50</td>
      <td>0.701 ms</td>
      <td>0.010 ms</td>
      <td>0.009 ms</td>
      <td>0.703 ms</td>
      <td>0.676 ms</td>
      <td>0.713 ms</td>
      <td>1.40</td>
      <td>176.03 KB</td>
    </tr>
  </tbody>
</table>


## Dependencies
- [StackExchange.Redis](https://github.com/StackExchange/StackExchange.Redis)