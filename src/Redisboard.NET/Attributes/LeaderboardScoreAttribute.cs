namespace Redisboard.NET.Attributes;

/// <summary>
/// Marks property that supplies ranking score for a leaderboard entity.
/// </summary>
/// <remarks>
/// <para>
/// Apply this attribute to exactly one property on each <see cref="Interfaces.ILeaderboardEntity"/>
/// implementation.
/// </para>
/// <para>
/// Supported property types are <see cref="double"/>, <see cref="float"/>, <see cref="int"/>, and
/// <see cref="long"/>.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class LeaderboardScoreAttribute : Attribute
{
}
