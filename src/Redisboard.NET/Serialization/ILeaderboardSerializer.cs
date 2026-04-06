namespace Redisboard.NET.Serialization;

/// <summary>
/// Defines serialization and deserialization behaviour used by the leaderboard
/// when persisting entity state to and from Redis.
/// Implement this interface to substitute the default <see cref="MemoryPackLeaderboardSerializer"/>
/// with a custom serializer (e.g. System.Text.Json, Newtonsoft.Json, MessagePack).
/// </summary>
public interface ILeaderboardSerializer
{
    /// <summary>Serializes <paramref name="value"/> to a byte array.</summary>
    byte[] Serialize<T>(T value);

    /// <summary>Deserializes the byte array <paramref name="data"/> into an instance of <typeparamref name="T"/>.</summary>
    T Deserialize<T>(byte[] data);
}
