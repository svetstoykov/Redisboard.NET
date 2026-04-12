namespace Redisboard.NET.Serialization;

/// <summary>
/// Defines serialization behavior for leaderboard entity metadata.
/// </summary>
/// <remarks>
/// Implement this interface to replace <see cref="MemoryPackLeaderboardSerializer"/> when entity metadata
/// must use a different binary or text format.
/// </remarks>
public interface ILeaderboardSerializer
{
    /// <summary>
    /// Serializes a value into a byte array.
    /// </summary>
    /// <typeparam name="T">Type of value to serialize.</typeparam>
    /// <param name="value">Value to serialize.</param>
    /// <returns>Serialized representation of <paramref name="value"/> suitable for Redis storage.</returns>
    byte[] Serialize<T>(T value);

    /// <summary>
    /// Deserializes a byte array into a value.
    /// </summary>
    /// <typeparam name="T">Target type to materialize.</typeparam>
    /// <param name="data">Serialized bytes previously produced for <typeparamref name="T"/>.</param>
    /// <returns>Deserialized instance represented by <paramref name="data"/>.</returns>
    T Deserialize<T>(byte[] data);
}
