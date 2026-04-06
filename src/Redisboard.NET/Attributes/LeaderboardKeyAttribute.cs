namespace Redisboard.NET.Attributes;

/// <summary>
/// Marks the property on an <see cref="Interfaces.ILeaderboardEntity"/> implementation
/// that uniquely identifies the entity within a leaderboard.
/// The property type must be assignable to <see cref="string"/>, <see cref="Guid"/>,
/// <see cref="int"/>, <see cref="long"/>, or <see cref="StackExchange.Redis.RedisValue"/>.
/// </summary>
/// <remarks>
/// Exactly one property per type must carry this attribute.
/// The library uses it to read the entity key when writing to Redis and
/// to re-populate the property on read-back.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class LeaderboardKeyAttribute : Attribute
{
}
