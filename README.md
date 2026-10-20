# Redisboard.NET 🚀

[![CI](https://github.com/svetstoykov/Redisboard.NET/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/svetstoykov/Redisboard.NET/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Redisboard.NET)](https://www.nuget.org/packages/Redisboard.NET/)
[![License](https://img.shields.io/github/license/svetstoykov/Redisboard.NET)](https://github.com/svetstoykov/Redisboard.NET/blob/main/LICENSE)

### A high-performance .NET Library for creating and interacting with Leaderboards using Redis.

Redisboard.NET is an optimized .NET library designed to handle leaderboards efficiently using Redis. It provides a simple, yet powerful API to create and manage leaderboards with various ranking systems. The library leverages Redis sorted sets for performance and uses LUA scripts for advanced querying capabilities.

> **Want to understand how it works under the hood?**
> Read the [Architecture Deep Dive](https://github.com/svetstoykov/Redisboard.NET/blob/main/docs/deep-dive.md) — covering the three-structure Redis design, score negation, Lua atomicity, ranking algorithms, serialization, and common pitfalls. A good starting point whether you're evaluating the library or contributing to it.

## Quick Start
**Requires Redis 7.0 or newer** for read-only Lua scripts (`EVAL_RO`). Redis Cluster requires the opt-in key configuration below and database zero.

First up, let's get a Redis server ready. Docker makes this super easy.

After you've got Docker [set up on your machine](https://docs.docker.com/engine/install/), just pop open your terminal and run:
```sh
docker run --name my-redis-server -p 6379:6379 -d redis:7
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
var leaderboard = new Leaderboard<Player>(redis, new MemoryPackLeaderboardSerializer());

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

Example result:

```text
Rank  Player   Points
1     Bob      150
2     Maya     125
3  -> player1 100
4     Sam      90
5     Nina     80
```


### **Dependency Injection**
Register the Leaderboard in your `IServiceCollection` using the built-in extension method:

`AddLeaderboard` also registers an `IConnectionMultiplexer` internally when you provide a config delegate and no existing Redis services are already registered. The `cfg` object is StackExchange.Redis `ConfigurationOptions`, so any supported connection settings can be configured there.

```cs
// Add to IServiceCollection
builder.Services.AddLeaderboard<Player>(cfg =>
{
    cfg.EndPoints.Add("localhost:6379");
    cfg.ClientName = "Development";
    cfg.DefaultDatabase = 5;
});
```

*\* Omit config delegate if you have already registered your `IConnectionMultiplexer` or `IDatabase` (ref. [StackExchange.Redis](https://github.com/StackExchange/StackExchange.Redis)). `AddLeaderboard` prefers `IConnectionMultiplexer` when both are registered. If it falls back to `IDatabase`, the `databaseIndex` argument is ignored because the database has already been selected.*

Supplying another config delegate after Redis has already been registered throws `ArgumentException` at registration. Register additional entity types without a delegate to reuse that connection.

See StackExchange.Redis configuration docs for more `ConfigurationOptions` settings: [Basics](https://stackexchange.github.io/StackExchange.Redis/Basics.html) and [Configuration](https://stackexchange.github.io/StackExchange.Redis/Configuration.html).

`databaseIndex` defaults to `-1`, honoring `ConfigurationOptions.DefaultDatabase`. Pass an explicit index, including `0`, to override it. Each entity registration retains its own serializer.

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

Other common APIs:

```cs
// Add many players in one batch operation.
await leaderboard.AddEntitiesAsync(leaderboardKey, players);

// Updates on missing players are no-ops. Use AddEntityAsync for upserts.
// Update only score for an existing player with model
await leaderboard.UpdateEntityScoreAsync(
    leaderboardKey,
    new Player { Id = "player1", Score = 175 });

// Update only score for an existing player by key and score
await leaderboard.UpdateEntityScoreAsync(
    leaderboardKey, entityKey: "player1", score: 175);;

// Update metadata without changing rank or score.
await leaderboard.UpdateEntityMetadataAsync(
    leaderboardKey,
    new Player { Id = "player1", Username = "Alice", AvatarUrl = "avatar.png" });

// Get players between rank 1 and rank 10.
var topTen = await leaderboard.GetEntitiesByRankRangeAsync(
    leaderboardKey, 1, 10, RankingType.Default);

// Get players whose scores fall within an inclusive range.
var scoreRange = await leaderboard.GetEntitiesByScoreRangeAsync(
    leaderboardKey, 80, 150, RankingType.Default);

// Get single player's current score.
var playerScore = await leaderboard.GetEntityScoreAsync(leaderboardKey, "player1");

// Get single player's current rank.
var playerRank = await leaderboard.GetEntityRankAsync(
    leaderboardKey, "player1", RankingType.Default);

// Get total number of players in leaderboard.
var totalPlayers = await leaderboard.GetSizeAsync(leaderboardKey);

// Delete one player from leaderboard.
await leaderboard.DeleteEntityAsync(leaderboardKey, "player1");

// Delete many players in one batch.
await leaderboard.DeleteEntitiesAsync(leaderboardKey, ["player1", "player2"]);

// Delete entire leaderboard and all associated Redis data.
await leaderboard.DeleteAsync(leaderboardKey);
```

## Redis key configuration

Default settings preserve existing key names. Configure a literal prefix to isolate environments or tenants:

```cs
var options = new LeaderboardOptions
{
    KeyPrefix = "production:",
    UseClusterHashTags = true
};
var leaderboard = new Leaderboard<Player>(redis, new MemoryPackLeaderboardSerializer(), options);

// With an existing Redis service registration:
builder.Services.AddLeaderboard<Player>(optionsAction: null, leaderboardOptions: options);
```

Cluster mode assigns all three structures for each leaderboard to one hash slot. The generated tag uses a SHA-256 digest of the leaderboard identity, so braces in caller-supplied identities cannot override it. Prefixes cannot contain braces in cluster mode. Different boards distribute across cluster slots; identical board identities use the same slot across prefixes.

Settings are copied at construction or DI registration. Changing prefix or cluster mode selects different Redis keys: **existing data is not migrated**. Keep existing settings to retain access, or migrate all three structures together during a controlled cutover. Default mode preserves `sorted_set_leaderboard_<identity>`, `entity_data_hashset_leaderboard_<identity>`, and `sorted_set_unique_score_<identity>`.

## Atomic score writes

Use atomic arithmetic instead of reading a score, changing it in application code, then writing it back.
Concurrent increments cannot overwrite one another. Negative deltas decrement; the explicit decrement
method takes a non-negative amount. Arithmetic requires an existing entity with metadata. Missing or
incomplete entities remain unchanged, returning `Applied = false` with null score and rank.

```cs
var earned = await leaderboard.IncrementEntityScoreAsync(leaderboardKey, "player1", 10);
var spent = await leaderboard.DecrementEntityScoreAsync(leaderboardKey, "player1", 5);

// Retain personal best without a read/write race; preserve metadata.
var best = await leaderboard.UpdateEntityScoreConditionalAsync(
    leaderboardKey, "player1", 200, ScoreUpdateCondition.Higher, RankingType.DenseRank);

// Insert only when absent, including metadata.
var joined = await leaderboard.AddEntityConditionalAsync(
    leaderboardKey, new Player { Id = "player3", Score = 10, Username = "Cara" },
    ScoreUpdateCondition.NotExists);
```

`ScoreUpdateCondition` supports `Always`, `Exists`, `NotExists`, `Higher`, and `Lower`.
Higher/lower compare scores strictly, so equal values reject both. Conditional entity writes insert
absent entities for `Always`, `NotExists`, `Higher`, and `Lower`; accepted replacements update both score
and metadata. Conditional score-only writes always require an existing complete entity.

Each operation returns `ScoreUpdateResult` (`Redisboard.NET.Models`) containing `Applied`, `Score`,
and one-based `Rank`, captured in the same Redis script as the write. Rejected conditions return the
unchanged score and rank; accepted equal-score writes still report `Applied = true`. Results use the
requested `RankingType`, defaulting to ordinal ranking. Import `Redisboard.NET.Enumerations` for both enums.

Scores must remain finite and non-negative. Invalid deltas, arithmetic overflow, and negative results
throw `ArgumentOutOfRangeException` before modifying ranking data or its dense-score index. Cancellation
is checked before dispatch; after dispatch the operation reports its Redis result. Existing write APIs
retain their behavior and signatures.

Applications providing custom `ILeaderboard<TEntity>` implementations must implement the four new
members: `IncrementEntityScoreAsync`, `DecrementEntityScoreAsync`,
`UpdateEntityScoreConditionalAsync`, and `AddEntityConditionalAsync`. This interface expansion is an
intentional compatibility change from 1.0.3. Package validation suppresses only the corresponding
member-specific CP0006 diagnostics for both target frameworks; other compatibility checks remain active.

## Entity lookups, pages, and expiry

```cs
var player = await leaderboard.GetEntityAsync(leaderboardKey, "player1", RankingType.DenseRank);
var friends = await leaderboard.GetEntitiesByKeysAsync(
    leaderboardKey, ["player3", "player1", "missing"], RankingType.DenseRank);
var page = await leaderboard.GetPageAsync(leaderboardKey, pageNumber: 3, pageSize: 20);
Console.WriteLine($"{page.Entities.Length} rows of {page.TotalCount}");

await leaderboard.ExpireAsync(leaderboardKey, TimeSpan.FromDays(7));
await leaderboard.ExpireAsync(leaderboardKey, null); // Remove expiry.
```

Each read returns payloads, current scores, and global ranks from one Redis snapshot.
A missing single entity returns `null`. Batch lookups preserve request order and duplicate
keys, omit missing members, and accept 1–10,000 keys. Pages use one-based page numbers
and physical positions, so ties never change page size; displayed ranks still use the
requested ranking mode. Page size is limited to 10,000. Empty pages retain the total
count of ranked members, including legacy entries whose metadata is missing.

Expiry applies one server-time deadline to all existing structures in one atomic script.
`null` removes expiry; zero deletes the board; negative durations are rejected. Positive
lifetimes round up to milliseconds. Missing boards are not created. Writes retain each
existing key’s TTL, but newly created keys do not inherit a previously set deadline.
Populate seasonal boards before setting expiry, and reapply expiry after recreating keys.

Custom `ILeaderboard<TEntity>` implementations must also implement `GetEntityAsync`,
`GetEntitiesByKeysAsync`, `GetPageAsync`, and `ExpireAsync`. These additions intentionally
expand the 1.0.3 interface; package validation suppresses only their specific CP0006 diagnostics.

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

builder.Services.AddLeaderboard<Player>(cfg => { /* ... */ }, serializer: new SystemTextJsonSerializer());

// Or via manually created object

var leaderboard = new Leaderboard<Player>(redis, new SystemTextJsonSerializer());
```

### DemoAPI
This repository also includes a very simple API project, which shows how you can setup the Leaderboard.  You can find the project [here](
https://github.com/svetstoykov/Redisboard.NET/tree/main/src/Redisboard.NET.DemoAPI)

## Ranking
Ranks are calculated when you query data. Redisboard.NET stores leaderboard scores in a Redis [Sorted Set](https://redis.io/docs/latest/develop/data-types/sorted-sets/) and entity metadata in a Redis [Hash](https://redis.io/docs/latest/develop/data-types/hashes/). It then uses Lua scripts to read both efficiently and apply the selected ranking rules.

This design keeps writes simple and makes reads fast. In our benchmarks, querying a leaderboard with more than 500,000 players stays under 1 ms for the common read paths shown below.

Every read API that returns ranks requires a `RankingType`. That value controls how ties are handled.

### 1. Default Ranking 🏆
Players are ordered by score first. If two or more players have the same score, Redis uses the member key as a tie-breaker and orders those tied players **lexicographically**. Each player still gets a unique rank number, so no rank numbers are shared or skipped.

This is the default ordering behavior of a Redis Sorted Set.

**Example:**
```
Scores: [{John, 100}, {Micah, 100}, {Alex, 99}, {Tim, 1}]
Ranks: [1, 2, 3, 4]
```


### 2. [Dense Rank](https://en.wikipedia.org/wiki/Ranking#Dense_ranking_(%221223%22_ranking)) 🥇🥈
Players with the same score receive the same rank. The next distinct score receives the next rank number with no gaps.

**Example:**
```
Scores: [100, 80, 50, 50, 40, 10]
Ranks: [1, 2, 3, 3, 4, 5]
```


### 3. [Standard Competition](https://en.wikipedia.org/wiki/Ranking#Standard_competition_ranking_(%221224%22_ranking)) 🏅
Players with the same score receive the same rank. The next distinct score skips ahead by the number of tied players.

**Example:**
```
Scores: [100, 80, 50, 50, 40, 10]
Ranks: [1, 2, 3, 3, 5, 6]
```


### 4. [Modified Competition](https://en.wikipedia.org/wiki/Ranking#Modified_competition_ranking_(%221334%22_ranking)) 🎖️
Players with the same score receive the same rank, but the gap appears before the tied group instead of after it.

**Example:**
```
Scores: [100, 80, 50, 50, 40, 10]
Ranks: [1, 2, 4, 4, 5, 6]
```

Use `RankingType.Default` when you want Redis native ordering and a unique position for every player. Use one of the other ranking types when tied scores should share the same displayed rank.

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
- [MemoryPack](https://github.com/Cysharp/MemoryPack)
- [StackExchange.Redis](https://github.com/StackExchange/StackExchange.Redis)
- [Microsoft.Extensions.DependencyInjection.Abstractions](https://github.com/dotnet/runtime/tree/main/src/libraries/Microsoft.Extensions.DependencyInjection.Abstractions)
