using StackExchange.Redis;

namespace Redisboard.NET.Exceptions;

/// <summary>
/// Raised when an optimistic concurrency version check fails during a score update.
/// </summary>
/// <remarks>
/// This exception indicates the entity was modified by another writer between the caller's read
/// and their versioned update attempt. The caller should re-read the entity and retry.
/// </remarks>
public sealed class ConcurrencyException : Exception
{
    /// <summary>
    /// Gets the entity key whose version check failed.
    /// </summary>
    public RedisValue EntityKey { get; }

    /// <summary>
    /// Gets the version the caller expected to find.
    /// </summary>
    public long ExpectedVersion { get; }

    /// <summary>
    /// Gets the version actually stored in Redis at time of conflict, or <see langword="null"/> when unknown.
    /// </summary>
    public long? ActualVersion { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ConcurrencyException"/> with entity and version details.
    /// </summary>
    /// <param name="entityKey">The entity key whose version check failed.</param>
    /// <param name="expectedVersion">The version the caller expected to find.</param>
    /// <param name="actualVersion">The version actually stored in Redis, if known.</param>
    public ConcurrencyException(RedisValue entityKey, long expectedVersion, long? actualVersion)
        : base($"Concurrency conflict for entity '{entityKey}': expected version {expectedVersion}, actual version {actualVersion?.ToString() ?? "unknown"}.")
    {
        EntityKey = entityKey;
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }
}
