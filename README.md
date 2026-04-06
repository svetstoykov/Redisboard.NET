# Redisboard.NET 🚀

### A high-performance .NET Library for creating and interacting with Leaderboards using Redis.

Redisboard.NET is a optimized .NET library designed to handle leaderboards efficiently using Redis. It provides a simple, yet powerful API to create and manage leaderboards with various ranking systems. The library leverages Redis sorted sets for performance and uses LUA scripts for advanced querying capabilities.

## 🚧 UNDER CONSTRUCTION (not available on NuGet) 🚧

**TODO:**  
✅ Add integration tests  
✅ Add benchmarks  
❌ Distribute to NuGet 

## Quick Start
First up, let's get a Redis server ready. Docker makes this super easy.

After you've got Docker [set up on your machine](https://docs.docker.com/engine/install/), just pop open your terminal and run:
```sh
docker run --name my-redis-server -d redis
```

With your Redis server humming along, it's time to install the [Redisboard.NET](https://www.nuget.org/packages/Redisboard.NET/) package with this quick command:

```sh
dotnet add package Redisboard.NET
```

Define your leaderboard entity. It must:
- Implement `ILeaderboardEntity`
- Be `partial` and annotated with `[MemoryPackable]`
- Decorate exactly one property with `[LeaderboardKey]`
- Decorate exactly one property with `[LeaderboardScore]`

```cs
// Define a player class implementing ILeaderboardEntity
[MemoryPackable]
public partial class Player : ILeaderboardEntity
{
    [LeaderboardKey]
    public string Id { get; set; }

    [LeaderboardScore]
    public double Score { get; set; }

    public long Rank { get; set; }  // Populated automatically on reads

    // Additional properties are stored as metadata
    public string Username { get; set; }
    public string AvatarUrl { get; set; }
}
```

Start managing your leaderboard with the `Leaderboard<Player>` class:
```cs
const string leaderboardKey = "your_leaderboard_key"

// Initialize a Leaderboard by passing IDatabase or IConnectionMultiplexer
var leaderboard = new Leaderboard<Player>(redis);

var players = new[]
{
    new Player { Id = "player1", Score = 100, Username = "Alice" },
    new Player { Id = "player2", Score = 150, Username = "Bob" }
};

// Add players to the leaderboard
await leaderboard.AddEntityAsync(leaderboardKey, players[0]);
await leaderboard.AddEntityAsync(leaderboardKey, players[1]);

// Retrieve player and neighbors (offset is default 10)
var result = await leaderboard.GetEntityAndNeighboursAsync(
    leaderboardKey, "player1", RankingType.Default);
```

## Entity Requirements

### MemoryPack Serialization

The library uses **MemoryPack** by default for high-performance serialization. All entity types must be properly configured:

```cs
[MemoryPackable]  // Required: enables MemoryPack source generation
public partial class Player : ILeaderboardEntity
{
    [LeaderboardKey]   // Identifies the entity uniquely
    public string Id { get; set; }

    [LeaderboardScore] // Used for ranking
    public double Score { get; set; }

    public long Rank { get; set; }  // Auto-populated on reads
}
```

**Key requirements:**
1. **The class must be `partial`** — the source generator emits a companion file
2. **Every nested type needs `[MemoryPackable]`** — if `Player` has a `Stats` property, `Stats` must also be `[MemoryPackable] partial`
3. **Parameterless constructor** — MemoryPack needs either a parameterless constructor or a constructor whose parameter names match property names
4. **Inheritance requires explicit registration** — for polymorphic hierarchies:

```cs
[MemoryPackable]
[MemoryPackUnion(0, typeof(HumanPlayer))]
[MemoryPackUnion(1, typeof(BotPlayer))]
public abstract partial class Player { }

[MemoryPackable]
public partial class HumanPlayer : Player { }
```

### Supported Property Types

| Attribute | Supported Types |
|-----------|-----------------|
| `[LeaderboardKey]` | `string`, `Guid`, `int`, `long`, `RedisValue` |
| `[LeaderboardScore]` | `double`, `float`, `int`, `long` |

### Custom Serializer

If you cannot use MemoryPack, implement `ILeaderboardSerializer` and pass it to the `Leaderboard` constructor:

```cs
public class SystemTextJsonSerializer : ILeaderboardSerializer
{
    public byte[] Serialize<T>(T value)
        => JsonSerializer.SerializeToUtf8Bytes(value);

    public T Deserialize<T>(byte[] data)
        => JsonSerializer.Deserialize<T>(data)!;
}

builder.Services.AddLeaderboard<Player>(cfg => { /* ... */ }, new SystemTextJsonSerializer());

// Or via manually created object

var leaderboard = new Leaderboard<Player>(redis, new SystemTextJsonSerializer());
```

### **Dependency Injection** 
Register the Leaderboard in your `IServiceCollection` using the built-in extension method:

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

Once registered, inject `ILeaderboard<Player>` via the constructor:

``` cs
public class MyService
{
    private readonly ILeaderboard<Player> _leaderboard;

    public MyService(ILeaderboard<Player> leaderboard)
    {
        _leaderboard = leaderboard;
    }
    
    public async Task AddPlayersAsync(Player[] players)
    {
        const string leaderboardKey = "your_leaderboard_key"

        foreach (var player in players)
            await _leaderboard.AddEntityAsync(leaderboardKey, player);
    }
}
```

### DemoAPI
This repository also includes a very simple API project, which shows how you can setup the Leaderboard.  You can find the project [here](
https://github.com/svetstoykov/Redisboard.NET/tree/main/src/Redisboard.NET.DemoAPI)

## Ranking
The rankings of the players are calculated on the fly when querying the data. Under the hood we are using multiple Redis data structures ([Sorted Set](https://redis.io/docs/latest/develop/data-types/sorted-sets/) and [Hash](https://redis.io/docs/latest/develop/data-types/hashes/)) and LUA scripts for queryin. This provides us with a **response time of under 1ms for over 500k players** in a leaderboard.

When querying data, you need to specify the `RankingType`, which can be one of the following:

### 1. Default Ranking 🏆
Members are ordered by score first, and if there are ties in scores, they are then ordered **lexicographically**. There is no skipping in the records ranking.  

(*This is Redis Sorted Set default ranking style.*)

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

These benchmarks were run over a leaderboard with **500,000 entries** of type:
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

We benchmarked the most common read path: getting an entity and its neighbors with a relevant `offset`.*

*\*`offset` controls how many neighbors above and below the target entity are returned.*

### Latest run at a glance

Environment: **BenchmarkDotNet 0.15.8**, **.NET 10.0.5**, **Apple M3 (8 cores)**, **macOS Tahoe 26.3.1**.

What these benchmarks show:
- Querying a 500K leaderboard stays **sub-millisecond** across all tested ranking modes and offsets.
- **Default ranking** is the fastest baseline in every scenario.
- **Dense** and **Competition** ranking add ranking semantics with a moderate overhead, while still staying very fast.
- Memory usage scales predictably with `offset` (more neighbors returned = more allocation).

<table>
  <thead>
    <tr>
      <th>Method</th>
      <th>Offset</th>
      <th>Mean</th>
      <th>Max</th>
      <th>Allocated</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td>Default Ranking</td>
      <td>10</td>
      <td><strong>449.6 us</strong></td>
      <td>509.9 us</td>
      <td>26.1 KB</td>
    </tr>
    <tr>
      <td>Dense Ranking</td>
      <td>10</td>
      <td>489.1 us</td>
      <td>537.9 us</td>
      <td>27.57 KB</td>
    </tr>
    <tr>
      <td>Competition Ranking</td>
      <td>10</td>
      <td>515.0 us</td>
      <td>665.5 us</td>
      <td>27.51 KB</td>
    </tr>
    <tr style="background-color:rgba(51, 170, 51, .14)">
      <td>Default Ranking</td>
      <td>20</td>
      <td><strong>473.6 us</strong></td>
      <td>570.9 us</td>
      <td>50.25 KB</td>
    </tr>
    <tr style="background-color:rgba(51, 170, 51, .14)">
      <td>Dense Ranking</td>
      <td>20</td>
      <td>518.7 us</td>
      <td>556.1 us</td>
      <td>50.99 KB</td>
    </tr>
    <tr style="background-color:rgba(51, 170, 51, .14)">
      <td>Competition Ranking</td>
      <td>20</td>
      <td>526.8 us</td>
      <td>567.5 us</td>
      <td>50.86 KB</td>
    </tr>
    <tr style="background-color:rgba(90, 200, 240, .14)">
      <td>Default Ranking</td>
      <td>50</td>
      <td><strong>522.7 us</strong></td>
      <td>566.4 us</td>
      <td>118.48 KB</td>
    </tr>
    <tr style="background-color:rgba(90, 200, 240, .14)">
      <td>Dense Ranking</td>
      <td>50</td>
      <td>664.5 us</td>
      <td>706.3 us</td>
      <td>120.88 KB</td>
    </tr>
    <tr style="background-color:rgba(90, 200, 240, .14)">
      <td>Competition Ranking</td>
      <td>50</td>
      <td>773.8 us</td>
      <td>899.0 us</td>
      <td>121.55 KB</td>
    </tr>
  </tbody>
</table>


## Dependencies
- [StackExchange.Redis](https://github.com/StackExchange/StackExchange.Redis)
- [Microsoft.Extensions.DependencyInjection](https://github.com/dotnet/runtime/tree/main/src/libraries/Microsoft.Extensions.DependencyInjection)
