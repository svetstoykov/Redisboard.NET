---
name: testing-principles
description: >
  Apply best practices for writing unit and integration tests for Redisboard.NET.
  Use this skill whenever the user asks for help writing, reviewing, or structuring
  tests of any kind - including xUnit, NUnit, MSTest, Vitest, or any other framework.
  Trigger on phrases like "write a test", "add unit tests", "review my tests",
  "how should I test this", "help with integration tests", or any request to
  improve test coverage or test quality. Also trigger when the user shares existing
  test code that smells wrong or is hard to maintain - even if they don't explicitly
  ask for a review.
---

# Testing Principles

Canonical shared guidance also lives at `ai/skills/testing-principles.md`. Keep this Kilo skill aligned with that file so non-Kilo agents can follow the same behavior.

A decision-making and code-generation guide for writing high-quality unit and integration
tests. These five principles apply across all frameworks and languages but examples are
given in C#/.NET (xUnit assertions) to reflect the primary usage context in
Redisboard.NET.

---

## The Five Core Principles

### 1. Arrange-Act-Assert (AAA)

Every test has exactly three phases, clearly separated:

- **Arrange** - set up system under test (SUT), dependencies, and input data
- **Act** - invoke single operation being tested
- **Assert** - verify observable outcome

```csharp
[Fact]
public async Task AddEntityAsync_WhenEntityIsValid_PersistsEntityAndScore()
{
    // Arrange
    var leaderboard = CreateLeaderboard();
    var player = new Player { Id = "p1", Score = 120, Username = "alice" };

    // Act
    await leaderboard.AddEntityAsync("season-1", player);

    // Assert
    var stored = await leaderboard.GetEntityAsync("season-1", "p1", RankingType.Default);
    Assert.NotNull(stored);
    Assert.Equal(120, stored.Score);
}
```

**Smell to watch for:** setup and assertion mixed together, or assertions spread across
multiple logical phases. If three phases are not obvious, split test.

---

### 2. One Behavior Per Test

Each test should have single reason to fail. Name test after behavior, not method:

```
// Bad
Leaderboard_Test()

// Good
AddEntityAsync_WhenKeyAlreadyExists_OverwritesStoredMetadata()
GetEntityAndNeighboursAsync_WhenEntityMissing_ReturnsNull()
GetRangeAsync_WhenUsingDenseRank_AssignsSharedRanksForTiedScores()
```

Multiple `Assert` calls are fine if they verify same behavior from different angles.
Do not combine unrelated behaviors in one test.

---

### 3. Test Behavior, Not Implementation

Assert on observable outcomes - returned entities, persisted Redis state, computed
ranks, thrown exceptions, serialized values - not internal call counts or private steps.

```csharp
// Bad: tests implementation detail
mockDatabase.Verify(x => x.SortedSetAddAsync(...), Times.Once);

// Good: tests observable behavior
var result = await leaderboard.GetEntityAsync("season-1", "p1", RankingType.Default);
Assert.NotNull(result);
Assert.Equal(1, result.Rank);
```

Prefer fakes and controlled integration environments over mocks when possible. Mocks
lock tests to implementation shape. Behavior-focused tests survive refactors.

---

### 4. Isolation and Determinism

| Layer | Isolation strategy |
|---|---|
| **Unit** | No network or Redis I/O. Inject dependencies. Use fakes/stubs for time, serialization, or scripts. |
| **Integration** | Use real Redis with isolated keys or database state owned by test. Each test creates and cleans its own data. |

```csharp
public sealed class FakeClock : ISystemClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
}
```

Flaky tests are unacceptable. If ranking depends on ordering rules, seed exact data and
assert exact rank outcomes. If Redis state can leak between tests, isolate by prefixing
keys with test-specific IDs.

---

### 5. Meaningful Failure Messages

When test fails in CI, failure output must explain problem without opening source code.

- Prefer xUnit's built-in `Assert` APIs
- Name tests with `Subject_Scenario_ExpectedOutcome`
- Mirror test project structure to source project structure
- Keep assertions explicit and local to tested behavior

```csharp
Assert.Equal(new long[] { 1, 2, 2, 4 }, result.Select(x => x.Rank).ToArray());
```

---

## Decision Guide: Which Layer to Use

| Question | Unit | Integration |
|---|---|---|
| Pure ranking math or normalization logic? | ✅ | - |
| Attribute discovery or serializer selection logic? | ✅ | - |
| Lua script result mapping? | ✅ | ✅ |
| Redis sorted set and hash interaction? | - | ✅ |
| Dependency injection registration wiring? | - | ✅ |
| End-to-end package behavior against real Redis? | - | ✅ |

Bias toward integration tests for Redisboard.NET public behavior, because core value of
library lives in Redis interaction, ranking semantics, and serialization boundaries.
Use unit tests for pure logic, guard clauses, ranking helpers, and mapping code.

---

## Test Data Builders

When Arrange blocks grow beyond 5-6 lines, extract builders with sane defaults. This is
especially useful for leaderboard entities, test key generation, and repeated ranking
scenarios.

```csharp
public sealed class PlayerBuilder
{
    private string _id = "player-1";
    private double _score = 100;
    private string _username = "alice";

    public PlayerBuilder WithId(string id) { _id = id; return this; }
    public PlayerBuilder WithScore(double score) { _score = score; return this; }
    public PlayerBuilder WithUsername(string username) { _username = username; return this; }

    public Player Build() => new()
    {
        Id = _id,
        Score = _score,
        Username = _username
    };
}
```

Usage:

```csharp
[Fact]
public async Task GetRangeAsync_WhenScoresTie_AssignsDenseRanksCorrectly()
{
    // Arrange
    var leaderboard = CreateLeaderboard();
    var players = new[]
    {
        new PlayerBuilder().WithId("a").WithScore(100).Build(),
        new PlayerBuilder().WithId("b").WithScore(80).Build(),
        new PlayerBuilder().WithId("c").WithScore(80).Build(),
        new PlayerBuilder().WithId("d").WithScore(40).Build()
    };

    foreach (var player in players)
        await leaderboard.AddEntityAsync("season-1", player);

    // Act
    var result = await leaderboard.GetRangeAsync("season-1", 0, 3, RankingType.DenseRank);

    // Assert
    Assert.Equal(new long[] { 1, 2, 2, 3 }, result.Select(x => x.Rank).ToArray());
}
```

Pattern stays same: defaults for everything, fluent overrides for relevant values, and
`Build()` returns test-ready data.

---

## Specification Tests (Cross-Implementation Contract Testing)

When multiple implementations must obey same behavioral contract, define abstract test
base once and let each implementation subclass it. This works well for serializers,
ranking strategies, or any interchangeable internal abstraction.

```csharp
public abstract class LeaderboardSerializerContractTests
{
    protected abstract ILeaderboardSerializer CreateSerializer();

    [Fact]
    public void SerializeAndDeserialize_RoundTripsEntity()
    {
        // Arrange
        var serializer = CreateSerializer();
        var entity = new PlayerBuilder().WithId("p1").WithScore(42).Build();

        // Act
        var bytes = serializer.Serialize(entity);
        var result = serializer.Deserialize<Player>(bytes);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(entity.Id, result.Id);
        Assert.Equal(entity.Score, result.Score);
        Assert.Equal(entity.Username, result.Username);
    }
}

public sealed class MemoryPackLeaderboardSerializerContractTests
    : LeaderboardSerializerContractTests
{
    protected override ILeaderboardSerializer CreateSerializer()
        => new MemoryPackLeaderboardSerializer();
}
```

This prevents drift. New implementation gets full contract suite immediately.

---

## Conditional Test Skipping

Some integration tests depend on runtime conditions such as Redis availability, Docker,
or platform-specific behavior. Use conditional skipping to make these constraints clear
instead of failing with unrelated connection errors.

```csharp
[Fact(Skip = "Requires local Redis instance")]
public Task Leaderboard_WithRealRedis_BehavesCorrectly()
    => Task.CompletedTask;
```

For dynamic checks, use helper methods or framework-specific skip support:

```csharp
public static class TestEnvironment
{
    public static string? RedisConnectionString =>
        Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING");

    public static bool HasRedis => !string.IsNullOrWhiteSpace(RedisConnectionString);
}

[Fact]
public async Task AddEntityAsync_WithConfiguredRedis_PersistsValue()
{
    if (!TestEnvironment.HasRedis)
    {
        return;
    }

    using var connection = await ConnectionMultiplexer.ConnectAsync(TestEnvironment.RedisConnectionString!);
    // ... integration test
}
```

Use conditional skipping for:

- Redis-dependent integration tests
- Platform-specific behavior
- Slow benchmark-like validation that should not run in standard CI

Do not use skipping to hide broken tests.

---

## Shared Fixtures for Expensive Setup

Reuse expensive setup with `IClassFixture<T>` when creation cost is real and fixture can
stay read-only. Good examples: Redis connection multiplexers, seeded test databases, or
shared serializer fixtures.

```csharp
public sealed class RedisFixture : IAsyncLifetime
{
    public IConnectionMultiplexer ConnectionMultiplexer { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        ConnectionMultiplexer = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
    }

    public async Task DisposeAsync()
    {
        await ConnectionMultiplexer.DisposeAsync();
    }
}

public sealed class LeaderboardIntegrationTests : IClassFixture<RedisFixture>
{
    private readonly RedisFixture _fixture;

    public LeaderboardIntegrationTests(RedisFixture fixture)
    {
        _fixture = fixture;
    }
}
```

Rules:

- Use fixtures for read-only shared infrastructure, not shared mutable state
- Prefer per-test keys over resetting whole Redis instances between tests
- Keep fixture setup obvious and minimal

---

## Separate Mutation from Verification

After mutating state, verify through public API or independent read path. Do not assert
against same in-memory object you just wrote.

```csharp
// Bad
var player = new PlayerBuilder().Build();
await leaderboard.AddEntityAsync("season-1", player);
Assert.Equal(100, player.Score);

// Good
var player = new PlayerBuilder().Build();
await leaderboard.AddEntityAsync("season-1", player);
var stored = await leaderboard.GetEntityAsync("season-1", player.Id, RankingType.Default);
Assert.NotNull(stored);
Assert.Equal(100, stored.Score);
```

This catches serialization bugs, mapping bugs, and Redis persistence bugs that trivial
in-memory assertions miss.

---

## Common Smells and Fixes

| Smell | Fix |
|---|---|
| Test name is method name | Rename to behavior-based test name |
| Arrange block is huge | Extract builder or fixture |
| Test verifies Redis API call count | Assert persisted Redis or public API result instead |
| Tests share hard-coded Redis keys | Prefix keys with unique test IDs |
| Ranking test uses random scores | Use fixed values and assert exact ranks |
| Integration test depends on leftover state | Create and clean isolated keys per test |
| Same serializer contract copied in multiple files | Extract abstract contract test base |
| Test silently passes when Redis missing | Skip explicitly so reports stay honest |

---

## Framework Quick Reference

### .NET (xUnit + NSubstitute)

```csharp
var serializer = Substitute.For<ILeaderboardSerializer>();

Assert.Equal(expected.Id, result.Id);
Assert.Equal(expected.Score, result.Score);

Assert.Equal(new long[] { 1, 2, 2, 4 }, ranks);
```

---

## Checklist Before Submitting Tests

- [ ] Test name describes behavior and expected outcome
- [ ] AAA structure is obvious
- [ ] One behavior per test
- [ ] Unit tests avoid I/O
- [ ] Integration tests own their Redis state
- [ ] Time, randomness, and external inputs are controlled
- [ ] Assertions verify observable behavior, not implementation details
- [ ] Tests pass in isolation and in parallel
- [ ] Shared contracts use abstract test bases where useful
- [ ] Builders are used when Arrange becomes noisy
- [ ] Redis-dependent tests skip or isolate cleanly when environment requires it
