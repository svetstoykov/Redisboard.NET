namespace Redisboard.NET.Serialization;

/// <summary>
/// Defines serialization and deserialization behaviour used by the leaderboard
/// when persisting entity state to and from Redis.
/// Implement this interface to substitute the default <see cref="SystemTextJsonLeaderboardSerializer"/>
/// with a custom serializer (e.g. Newtonsoft.Json, MessagePack).
/// </summary>
public interface ILeaderboardSerializer
{
    /// <summary>Serializes <paramref name="value"/> to a UTF-8 string.</summary>
    string Serialize<T>(T value);

    /// <summary>Deserializes the UTF-8 <paramref name="json"/> string into an instance of <typeparamref name="T"/>.</summary>
    T Deserialize<T>(string json);
}
