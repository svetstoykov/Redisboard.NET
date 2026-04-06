namespace Redisboard.NET.Attributes;

/// <summary>
/// Marks the property on an <see cref="Interfaces.ILeaderboardEntity"/> implementation
/// that holds the entity's numeric score used for ranking.
/// The property type must be one of <see cref="double"/>, <see cref="float"/>,
/// <see cref="int"/>, or <see cref="long"/>.
/// </summary>
/// <remarks>
/// Exactly one property per type must carry this attribute.
/// The library reads the score when writing to Redis and writes it back
/// when deserialising query results.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class LeaderboardScoreAttribute : Attribute
{
}
