using Redisboard.NET.Common.Models;

namespace Redisboard.NET.Tests.Integration;

internal static class LeaderboardSeed
{
    public static Player Player(string id, double score)
        => new()
        {
            Id = id,
            Score = score
        };

    public static IEnumerable<(string key, double score)> Sequence(string prefix, int startInclusive, int count)
    {
        for (var index = 0; index < count; index++)
        {
            var value = startInclusive + index;
            yield return ($"{prefix}{value}", value);
        }
    }
}
