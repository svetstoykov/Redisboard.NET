namespace Redisboard.NET.Attributes;

/// <summary>
/// Marks property that supplies unique Redis member key for a leaderboard entity.
/// </summary>
/// <remarks>
/// <para>
/// Apply this attribute to exactly one property on each <see cref="Interfaces.ILeaderboardEntity"/>
/// implementation.
/// </para>
/// <para>
/// Supported property types are <see cref="string"/>, <see cref="Guid"/>, <see cref="int"/>,
/// <see cref="long"/>, and <see cref="StackExchange.Redis.RedisValue"/>.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class LeaderboardKeyAttribute : Attribute
{
}
