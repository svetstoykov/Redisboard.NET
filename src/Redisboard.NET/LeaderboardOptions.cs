namespace Redisboard.NET;

/// <summary>Defines Redis key naming options for a leaderboard instance.</summary>
/// <remarks>
/// Options are copied when constructing <see cref="Leaderboard{TEntity}"/> or registering it with dependency injection.
/// Changing these settings selects a different Redis namespace; existing data is not migrated.
/// </remarks>
public sealed class LeaderboardOptions
{
    /// <summary>Gets or sets the literal prefix prepended to every Redis key.</summary>
    /// <remarks>
    /// Defaults to an empty string to preserve existing keys. Include any desired separator.
    /// Braces are not permitted when <see cref="UseClusterHashTags"/> is enabled because they could override the generated hash tag.
    /// </remarks>
    public string KeyPrefix { get; set; } = string.Empty;

    /// <summary>Gets or sets whether all keys for one leaderboard share a Redis Cluster hash tag.</summary>
    /// <remarks>
    /// Defaults to <see langword="false"/> to preserve existing keys. When enabled, a SHA-256 digest of the
    /// leaderboard identity becomes a hash tag before the original identity, preventing user-supplied braces from changing its slot.
    /// See <see href="https://redis.io/docs/latest/operate/oss_and_stack/reference/cluster-spec/#hash-tags"/>.
    /// </remarks>
    public bool UseClusterHashTags { get; set; }

    internal LeaderboardOptions Snapshot()
    {
        if (KeyPrefix is null)
            throw new ArgumentNullException(nameof(KeyPrefix));
        if (UseClusterHashTags && (KeyPrefix.Contains('{') || KeyPrefix.Contains('}')))
            throw new ArgumentException("Key prefixes cannot contain braces when cluster hash tags are enabled.", nameof(KeyPrefix));
        return new LeaderboardOptions { KeyPrefix = KeyPrefix, UseClusterHashTags = UseClusterHashTags };
    }
}
