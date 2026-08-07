namespace Redisboard.NET.Attributes;

/// <summary>
/// Marks a property that carries optimistic concurrency version for leaderboard entity score updates.
/// </summary>
/// <remarks>
/// <para>
/// Apply this attribute to at most one <see cref="long"/> property on <see cref="Interfaces.ILeaderboardEntity"/>
/// implementations. When present, versioned score update methods compare expected version against
/// the Redis-stored version and atomically increment on success.
/// </para>
/// <para>
/// This attribute is optional. Entities without version support cannot use the <c>WithVersion</c> overloads.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class LeaderboardVersionAttribute : Attribute
{
}
