namespace Redisboard.NET.Exceptions;

/// <summary>
/// Represents error raised when a leaderboard entity type is configured incorrectly.
/// </summary>
/// <remarks>
/// This exception reports invalid entity metadata such as missing ranking attributes, duplicate attribute
/// usage, or unsupported property types for key and score members.
/// </remarks>
public sealed class LeaderboardConfigurationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LeaderboardConfigurationException"/> class with an error message.
    /// </summary>
    /// <param name="message">Message that describes invalid entity configuration.</param>
    public LeaderboardConfigurationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LeaderboardConfigurationException"/> class with an error message and inner exception.
    /// </summary>
    /// <param name="message">Message that describes invalid entity configuration.</param>
    /// <param name="innerException">Underlying exception that caused configuration validation to fail.</param>
    public LeaderboardConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
