using Redisboard.NET.Enumerations;

namespace Redisboard.NET;

/// <summary>Defines Redis key naming and score ordering options for a leaderboard instance.</summary>
/// <remarks>
/// Options are copied when constructing <see cref="Leaderboard{TEntity}"/> or registering it with dependency injection.
/// Changing key settings selects a different Redis namespace; existing data is not migrated.
/// Changing score direction requires rebuilding existing data. All clients sharing keys must use the same direction.
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

    /// <summary>Gets or sets which score ranks first.</summary>
    /// <remarks>
    /// Defaults to <see cref="Enumerations.ScoreOrder.Descending"/> to preserve existing negated scores.
    /// <see cref="Enumerations.ScoreOrder.Ascending"/> stores scores unchanged. Direction applies to all writes and reads;
    /// equal scores always retain ascending Redis member-byte ordering.
    /// </remarks>
    public ScoreOrder ScoreOrder { get; set; } = ScoreOrder.Descending;

    internal LeaderboardOptions Snapshot()
    {
        if (ScoreOrder is not (ScoreOrder.Descending or ScoreOrder.Ascending))
            throw new ArgumentOutOfRangeException(nameof(ScoreOrder));
        if (KeyPrefix is null)
            throw new ArgumentNullException(nameof(KeyPrefix));
        if (UseClusterHashTags && (KeyPrefix.Contains('{') || KeyPrefix.Contains('}')))
            throw new ArgumentException("Key prefixes cannot contain braces when cluster hash tags are enabled.", nameof(KeyPrefix));
        return new LeaderboardOptions { KeyPrefix = KeyPrefix, UseClusterHashTags = UseClusterHashTags, ScoreOrder = ScoreOrder };
    }
}
