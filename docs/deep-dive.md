# Redisboard.NET — A Deep Dive

> How a .NET leaderboard library turns three Redis data structures and a handful of Lua scripts into sub-millisecond rankings for half a million players.

---

## The Problem

You're building a game. Players earn points. You need a leaderboard.

Sounds simple. Then reality hits:

- You have 500,000 concurrent players.
- Players update their scores constantly.
- Some players tie. Your product manager now wants four different ways to handle ties.
- The leaderboard page must load in under 100 ms.
- You also want to show each player their rank plus the ten players above and below them.

A SQL table with an `ORDER BY score DESC` query can do this — for a few thousand rows. At half a million, with frequent writes and reads happening simultaneously, the database becomes a bottleneck. You start adding indexes, caching layers, and read replicas. It gets complicated fast.

Redis was built for exactly this. It has a native data structure — the **Sorted Set** — that keeps members ordered by score at all times. Insertions, deletions, and rank lookups are all `O(log N)`. It lives entirely in memory. And it's wicked fast.

Redisboard.NET wraps Redis's sorted set primitives into a clean, strongly-typed .NET API, handles the tricky edge cases (ties, atomicity, metadata), and gives you a library you can drop into any .NET project.

---

## The Redis Sorted Set — The Foundation

Before diving into the library itself, you need to understand the data structure it's built on.

A Redis **Sorted Set** (`ZSET`) is a collection of unique string members, each associated with a floating-point score. Redis keeps the members sorted by score automatically. That's the whole magic trick.

```
# The Redis command to add a member with a score
ZADD leaderboard 4200 "alice"
ZADD leaderboard 3800 "bob"
ZADD leaderboard 4200 "carol"  # Same score as alice

# Get members by rank (0-indexed, ascending order)
ZRANGE leaderboard 0 -1 WITHSCORES
# Returns: bob(3800), alice(4200), carol(4200)

# Get a member's rank (0-indexed)
ZRANK leaderboard "alice"  # Returns 1 (second from bottom)
```

### Under the Hood — Skip Lists

You might wonder: how does Redis keep things sorted so fast? The answer is a data structure called a **skip list**.

A skip list is a layered linked list. The bottom layer holds every element in sorted order. Each layer above is a "fast lane" that skips over more and more elements — like express vs. local subway lines. To find an element, you start at the top (fewest stops) and drop down a level whenever you overshoot.

```
Level 3:  [HEAD] ────────────────────────────────────────── [3800] ──── [END]
Level 2:  [HEAD] ──────────────── [1500] ─────────────────  [3800] ──── [END]
Level 1:  [HEAD] ──── [900] ───── [1500] ──── [2200] ─────  [3800] ──── [END]
Level 0:  [HEAD] ─── [900] ─ [1100] ─ [1500] ─ [1900] ─ [2200] ─ [3000] ─ [3800] ─ [END]
           (base layer — every element, fully sorted)
```

A search for `3000` walks the express lanes first, skipping large chunks, then narrows in on the base layer — touching only a handful of nodes instead of scanning the whole list. This gives `O(log N)` for inserts, deletes, and rank lookups, no matter how many members are in the set.

This is exactly why sorted sets scale so well: a leaderboard with 500,000 players needs roughly 19 comparisons (`log₂ 500,000 ≈ 18.9`) to locate any member. A linear scan would need up to 500,000.

> For a deeper look at how Redis implements sorted sets internally, [this post](https://jothipn.github.io/2023/04/07/redis-sorted-set.html) walks through the skip list structure with great diagrams.

One catch: Redis sorts in **ascending** order by default. For a leaderboard, you want the *highest* score at the top. The common trick — and the one Redisboard.NET uses — is to **negate the scores** before storing them. A score of `4200` is stored as `-4200`. Now the highest real score has the lowest stored value, so it naturally appears first in ascending order.

```csharp
// Inside Leaderboard.cs — score is negated before every write
private static double ToRedisScore(double score) => -score;

// And un-negated on every read, so callers always see positive numbers
private static double FromRedisScore(double score) => -score;
```

This is a simple but important detail. If you ever inspect a leaderboard directly in Redis, all the scores will be negative — that's intentional.

---

## Three Structures, One Leaderboard

For each leaderboard, Redisboard.NET maintains **three Redis keys**:

### 1. The Leaderboard Sorted Set

```
Key: sorted_set_leaderboard_{leaderboardKey}
```

This is the source of truth for rankings. Members are entity keys (e.g., `"player:42"`), and scores are the negated real scores.

```
ZADD sorted_set_leaderboard_global -4200 "player:1"
ZADD sorted_set_leaderboard_global -3800 "player:2"
```

### 2. The Unique Scores Sorted Set

```
Key: sorted_set_unique_score_{leaderboardKey}
```

This is the clever bit. It's another sorted set, but it only stores *distinct* score values — not player IDs. The member is the score value as a string, and the score is the same negated value.

```
# If players have scores 4200, 3800, 3800 — only two unique scores exist
ZADD sorted_set_unique_score_global -4200 "-4200"
ZADD sorted_set_unique_score_global -3800 "-3800"
```

Why? Because `ZRANK` on this set gives you the **dense rank** of any score in O(log N) — without scanning all members. This is the key to implementing advanced ranking modes efficiently.

### 3. The Metadata Hash

```
Key: entity_data_hashset_leaderboard_{leaderboardKey}
```

A Redis hash that maps each entity key to its serialized binary payload. The sorted set only knows about keys and scores; any additional player data (username, avatar URL, level) lives here.

```
HSET entity_data_hashset_leaderboard_global "player:1" <binary blob>
```

This separation is intentional: score operations don't touch metadata, and metadata updates don't disturb rankings.

---

## The Ranking Problem

This is where it gets interesting. "What's my rank?" seems simple, but there are four different answers depending on what you mean.

Suppose five players have these scores:

| Player | Score |
|--------|-------|
| Alice  | 100   |
| Bob    | 90    |
| Carol  | 90    |
| Dave   | 80    |
| Eve    | 80    |

Bob and Carol are tied. Dave and Eve are tied. What rank is Bob? What rank is Dave?

**Default (1, 2, 3, 4, 5)** — Everyone gets a unique rank. Ties are broken lexicographically by key. Simple, no gaps, but ties feel arbitrary.

**Dense Rank (1, 2, 2, 3, 3)** — Tied players share a rank. The next distinct score gets the next integer. No gaps. This is what most people expect from a leaderboard.

**Standard Competition / "1224" (1, 2, 2, 4, 4)** — Tied players share the rank of the *first* player with that score. There's a gap after each tied group. This is how Olympic medals work.

**Modified Competition / "1334" (1, 3, 3, 4, 4)** — Wait, that's wrong. Let me redo: Tied players share the rank of the *last* player with that score. Dave and Eve would both be rank 4 (last position of the 80-score group). Bob and Carol would both be rank 3 (last position of the 90-score group). This variant is used in some academic grading systems.

Redisboard.NET implements all four, and the choice is a simple enum parameter on any read call:

```csharp
var topPlayers = await leaderboard.GetEntitiesByRankRangeAsync(
    leaderboardKey: "global",
    startRank: 1,
    endRank: 10,
    rankingType: RankingType.DenseRank  // or Default, StandardCompetition, ModifiedCompetition
);
```

---

## Lua Scripts — Keeping It Atomic

When you add a player to the leaderboard, three things need to happen:

1. Add them to the main sorted set.
2. Add their score to the unique scores set (if it isn't already there).
3. Store their metadata in the hash.

If the server crashes after step 1 but before step 3, your data is inconsistent. Redis is single-threaded, but that only protects individual commands — not a sequence of them.

The solution is **Lua scripts**. Redis executes a Lua script as a single atomic unit. No other command can run in between. No partial writes.

Redisboard.NET bundles five Lua scripts as embedded resources. Here's the batch add script, annotated:

```lua
-- KEYS[1]: main sorted set, KEYS[2]: unique scores set, KEYS[3]: metadata hash
local leaderboardKey = KEYS[1]
local uniqueScoreKey = KEYS[2]
local metadataKey = KEYS[3]

-- ARGV arrives in triplets: entityKey, invertedScore, serializedMetadata
for i = 1, #ARGV, 3 do
  local entityKey = ARGV[i]
  local invertedScore = tonumber(ARGV[i + 1])
  local metadata = ARGV[i + 2]

  -- All three writes happen atomically — no other command can interleave
  redis.call('ZADD', leaderboardKey, invertedScore, entityKey)
  redis.call('ZADD', uniqueScoreKey, invertedScore, tostring(invertedScore))
  redis.call('HSET', metadataKey, entityKey, metadata)
end

return 1
```

The delete script is more interesting because it needs to clean up the unique scores set carefully — only removing a score if no other player still has it:

```lua
for i = 1, #ARGV do
  local entityKey = ARGV[i]

  -- Check what score this player has before removing them
  local scoreStr = redis.call('ZSCORE', leaderboardKey, entityKey)

  redis.call('ZREM', leaderboardKey, entityKey)
  redis.call('HDEL', metadataKey, entityKey)

  -- Only remove the score from the unique set if nobody else has it
  if scoreStr ~= false then
    local countWithScore = redis.call('ZCOUNT', leaderboardKey, scoreStr, scoreStr)
    if countWithScore == 0 then
      redis.call('ZREM', uniqueScoreKey, scoreStr)
    end
  end
end
```

The dense rank query script shows how the unique scores set earns its keep:

```lua
-- Fetch members from the main leaderboard (paginated)
local zrangeResult = redis.call('ZRANGE', sortedSetCacheKey, startIndex, endIndex, 'WITHSCORES')

local result = {}

for i = 1, #zrangeResult, 2 do
    local memberIdentifier = zrangeResult[i]
    local memberScore = zrangeResult[i + 1]

    -- ZRANK on the unique-scores set gives the dense rank in O(log N)
    -- No need to scan all members to count distinct scores above this one
    local memberUniqueRank = redis.call('ZRANK', uniqueScoresSortedSetCacheKey, tostring(memberScore))

    table.insert(result, { memberIdentifier, memberUniqueRank + 1, memberScore })
end

return result
```

Without the unique scores set, you'd have to `ZRANGEBYSCORE` to count how many distinct scores exist above the current one — a much more expensive operation at scale.

Scripts are loaded lazily (once, on first use) and cached by StackExchange.Redis using script SHA hashing. Redis stores the compiled script internally and you just send the SHA on subsequent calls — smaller payloads, faster round trips.

---

## The .NET Side — Entity Design

On the C# side, you define your entity once and the library handles everything else.

```csharp
[MemoryPackable]  // Required for the default binary serializer
public partial class Player : ILeaderboardEntity
{
    [LeaderboardKey]   // Tells the library which property is the unique ID
    public string Id { get; set; }

    [LeaderboardScore] // Tells the library which property is the score
    public double Score { get; set; }

    // ILeaderboardEntity requires this — the library populates it on reads
    public long Rank { get; set; }

    // Everything else is just metadata, stored in the hash
    public string Username { get; set; }
    public string AvatarUrl { get; set; }
}
```

The `[LeaderboardKey]` and `[LeaderboardScore]` attributes are inspected once at startup using reflection. The library then compiles **expression trees** into delegate functions for property access — so there's zero reflection overhead at runtime. It's the same technique Entity Framework uses for its compiled queries.

```csharp
// EntityTypeAccessor.cs — conceptually what happens at startup
// A compiled getter is created once and reused on every entity
private static readonly Func<TEntity, string> _getKey =
    Expression.Lambda<Func<TEntity, string>>(
        Expression.Property(param, keyProperty), param
    ).Compile();
```

The accessor is cached statically per closed generic type (e.g., `Leaderboard<Player>`) — the CLR's type-per-T guarantee means this cache is free.

---

## Reading Back Ranks — The Full Flow

Here's what happens when you ask for a player's rank and their ten nearest neighbours:

```csharp
var results = await leaderboard.GetEntityAndNeighboursAsync(
    leaderboardKey: "global",
    entityKey: "player:42",
    offset: 10,                          // 10 players above and below
    rankingType: RankingType.DenseRank
);
```

Under the hood:

1. **`ZRANK`** — Find player 42's position in the main sorted set. O(log N).
2. **Lua script** — Fetch members from `(position - 10)` to `(position + 10)`, with scores. One round trip.
3. For each member, **`ZRANK`** on the unique scores set gives the dense rank. This happens inside the Lua script — still one round trip.
4. **`HMGET`** — Fetch metadata for all returned member keys. One round trip.
5. Deserialize each metadata blob, populate the `Rank` property, return the array.

Three round trips total for the full page, regardless of leaderboard size.

---

## Serialization — Fast Binary by Default

Metadata is serialized to bytes before being stored in the Redis hash. The default serializer uses **MemoryPack**, a source-generated binary serializer that is among the fastest available for .NET.

"Source-generated" means the serialization code is created at compile time by a Roslyn analyzer, not via runtime reflection. The result is code that looks hand-written — tight loops, direct field access, no boxing.

```csharp
// The abstraction — easy to swap out
public interface ILeaderboardSerializer
{
    byte[] Serialize<T>(T value);
    T Deserialize<T>(byte[] data);
}
```

If you need JSON (for human-readable debugging), or Protobuf (for cross-language compatibility), you just implement this interface and pass it in at startup.

```csharp
// Registering with a custom serializer
services.AddLeaderboard<Player>(
    optionsAction: opts => opts.EndPoints.Add("localhost:6379"),
    serializer: new SystemTextJsonLeaderboardSerializer()
);
```

---

## Dependency Injection — Drop-In Setup

The library ships with a `IServiceCollection` extension that wires up everything in one call:

```csharp
// In Program.cs / Startup.cs
services.AddLeaderboard<Player>(opts =>
{
    opts.EndPoints.Add("localhost:6379");
    opts.Password = "your-redis-password";
});

// Then inject wherever you need it
public class GameService(ILeaderboard<Player> leaderboard) { ... }
```

Under the hood this registers `IConnectionMultiplexer` (the StackExchange.Redis connection pool), `IDatabase`, and `ILeaderboard<Player>` — all as singletons, because StackExchange.Redis connections are designed to be long-lived and shared.

---

## Common Pitfalls

### 1. Forgetting `[MemoryPackable]` on Your Entity

MemoryPack requires both the attribute and the `partial` keyword on your class. If you miss either, you'll get a runtime exception when first trying to serialize — not a compile error. Add `partial` to your class declaration and `[MemoryPackable]` on top.

### 2. Deeply Nested Types Without `[MemoryPackable]`

If your `Player` entity has a `Inventory` property that is itself a class, that class also needs `[MemoryPackable]`. The error message from MemoryPack is usually clear, but it's easy to forget a level down.

### 3. Unbounded Batch Sizes

The library caps batch operations at **10,000 entities** per call. This is because Lua scripts receive all arguments in a single `ARGV` array, which is held in Redis memory for the duration of the script. Sending 100,000 entities at once would be a very large Lua call. If you need to seed a large leaderboard, chunk your data:

```csharp
foreach (var batch in players.Chunk(5000))
{
    await leaderboard.AddEntitiesAsync("global", batch);
}
```

### 4. Fire-and-Forget Is Not Guaranteed Delivery

Many write methods accept a `fireAndForget: true` flag. This tells StackExchange.Redis to send the command and not wait for an acknowledgement. It's faster, but if the connection drops at that exact moment, the write is silently lost. Only use it for non-critical updates (e.g., incrementing a "games played" counter) not for score changes that affect rankings.

```csharp
// Fine for casual stats
await leaderboard.UpdateEntityMetadataAsync("global", player, fireAndForget: true);

// Don't fire-and-forget for score changes
await leaderboard.UpdateEntityScoreAsync("global", player);
```

### 5. Scores Are Stored Negated — Don't Query Redis Directly for Diagnostics

If you open a Redis CLI and inspect the sorted set directly, all scores will be negative. A player with score `9500` will appear with score `-9500`. This is intentional — but confusing the first time you see it. Use the library's `GetEntityScoreAsync` method if you need the real value.

### 6. The Unique Scores Set Gets Out of Sync If You Write Directly to Redis

The three-structure design relies on all writes going through the library. If you manually `ZADD` to the main sorted set from a Redis CLI or another tool, the unique scores set won't be updated, and dense/competition ranking will return wrong results. Always write through the API.

---

## Lessons Worth Taking Away

**Score negation is a clean trick for inverting sort order.** Redis sorted sets always sort ascending. By negating values you invert the order without any extra configuration. This pattern shows up in other contexts too — if you ever need to `ORDER BY` something descending in a data structure that only sorts ascending, negate it.

**Maintain a secondary index for expensive queries.** The unique scores sorted set is a secondary index. It trades a small amount of write overhead (one extra `ZADD` per unique score) for dramatically cheaper reads when computing dense ranks. This is the classic read/write tradeoff — and it's a tradeoff worth making when reads vastly outnumber writes, as they do on a leaderboard.

**Lua scripts are your transaction primitive in Redis.** Redis has no multi-statement transactions in the traditional sense. `MULTI`/`EXEC` pipelines commands but doesn't allow you to read intermediate values and branch on them. Lua scripts do. Any time you need "read, decide, write" atomically in Redis, reach for Lua.

**Compile your reflection once, use it forever.** The expression-tree trick is worth knowing. Instead of calling `PropertyInfo.GetValue(obj)` on every entity, compile it into a typed delegate once. The JIT treats it like any other method call. The pattern generalises: any time you're paying reflection costs in a hot path, ask whether you can compile the reflection away.

**Keep scoring data and display data separate.** Storing only the score in the sorted set — and everything else in a hash — means your ranking logic never has to touch metadata. You can update a player's username without touching the sorted set at all. This separation of concerns is clean, and it scales: the sorted set stays lean and the hash can grow arbitrarily.

---

## Putting It All Together

Here's a minimal but realistic example: a game server that adds a player, gets their neighbours, and cleans up on departure.

```csharp
// Define your entity
[MemoryPackable]
public partial class Player : ILeaderboardEntity
{
    [LeaderboardKey]
    public string Id { get; set; }

    [LeaderboardScore]
    public double Score { get; set; }

    public long Rank { get; set; }

    public string Username { get; set; }
}

// Wire up in DI
services.AddLeaderboard<Player>(opts => opts.EndPoints.Add("redis:6379"));

// Use in your game service
public class MatchService(ILeaderboard<Player> leaderboard)
{
    private const string GlobalBoard = "global";

    public async Task RecordWin(string playerId, string username, double newScore)
    {
        var player = new Player { Id = playerId, Username = username, Score = newScore };
        await leaderboard.AddEntityAsync(GlobalBoard, player);
    }

    public async Task<Player[]> GetContextualRanking(string playerId)
    {
        // Returns the player plus 5 above and 5 below, with dense ranks
        return await leaderboard.GetEntityAndNeighboursAsync(
            GlobalBoard, playerId, offset: 5, RankingType.DenseRank);
    }

    public async Task RemovePlayer(string playerId)
    {
        await leaderboard.DeleteEntityAsync(GlobalBoard, playerId);
    }
}
```

Three methods. No Redis boilerplate. No score negation. No Lua scripts to manage. The library handles it — and at 500,000 players, it's still returning results in under a millisecond.

---

## Summary

Redisboard.NET is a focused library with a clear architecture:

- **Three Redis structures** per leaderboard: main sorted set for ranking, unique scores sorted set for efficient dense-rank lookups, and a metadata hash for display data.
- **Score negation** to make ascending Redis order behave like descending leaderboard order.
- **Lua scripts** for atomic multi-structure writes and reads — no partial updates, no race conditions.
- **Expression-tree-compiled property accessors** for zero-overhead reflection at runtime.
- **MemoryPack** for fast binary serialization of metadata, with a pluggable interface if you need something else.
- **Four ranking modes** (Default, Dense, Standard Competition, Modified Competition) covering every common leaderboard variant.

It's a good example of how a well-chosen data structure (the sorted set) combined with a secondary index (unique scores), atomic scripting (Lua), and thoughtful .NET integration can solve a genuinely hard performance problem in a way that feels simple from the outside.
