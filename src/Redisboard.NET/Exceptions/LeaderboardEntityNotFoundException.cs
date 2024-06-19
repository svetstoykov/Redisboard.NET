namespace Redisboard.NET.Exceptions;

public class LeaderboardEntityNotFoundException : Exception
{
    public LeaderboardEntityNotFoundException()
    {
    }

    public LeaderboardEntityNotFoundException(string message)
        : base(message)
    {
    }

    public LeaderboardEntityNotFoundException(string message, Exception inner)
        : base(message, inner)
    {
    }
}