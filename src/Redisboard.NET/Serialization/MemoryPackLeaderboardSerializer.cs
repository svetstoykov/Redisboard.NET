using MemoryPack;

namespace Redisboard.NET.Serialization;

/// <summary>
/// Represents <see cref="ILeaderboardSerializer"/> implementation backed by MemoryPack.
/// </summary>
/// <remarks>
/// <para>
/// This serializer uses <see cref="MemoryPackSerializer"/> for fast binary serialization of leaderboard
/// metadata.
/// </para>
/// <para>
/// Entity types must satisfy MemoryPack requirements, including appropriate annotations and generated code.
/// </para>
/// </remarks>
public sealed class MemoryPackLeaderboardSerializer : ILeaderboardSerializer
{
    /// <summary>
    /// Serializes a value with MemoryPack.
    /// </summary>
    /// <typeparam name="T">Type of value to serialize.</typeparam>
    /// <param name="value">Value to serialize.</param>
    /// <returns>Binary payload produced by <see cref="MemoryPackSerializer"/>.</returns>
    public byte[] Serialize<T>(T value)
        => MemoryPackSerializer.Serialize(value);

    /// <summary>
    /// Deserializes a MemoryPack payload into a value.
    /// </summary>
    /// <typeparam name="T">Target type to materialize.</typeparam>
    /// <param name="data">Binary payload previously produced for <typeparamref name="T"/>.</param>
    /// <returns>Deserialized instance represented by <paramref name="data"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when MemoryPack returns <see langword="null"/> for <typeparamref name="T"/>.</exception>
    public T Deserialize<T>(byte[] data)
        => MemoryPackSerializer.Deserialize<T>(data)
           ?? throw new InvalidOperationException($"Deserialization of type '{typeof(T).Name}' returned null.");
}
