namespace Redisboard.NET.Exceptions;

/// <summary>
/// Thrown when a type used as a leaderboard entity is not correctly configured.
/// Common causes include missing <see cref="Attributes.LeaderboardKeyAttribute"/> or
/// <see cref="Attributes.LeaderboardScoreAttribute"/>, ambiguous attribute usage,
/// or an unsupported property type for key or score.
/// </summary>
public sealed class LeaderboardConfigurationException : Exception
{
    /// <inheritdoc />
    public LeaderboardConfigurationException(string message)
        : base(message)
    {
    }

    /// <inheritdoc />
    public LeaderboardConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
