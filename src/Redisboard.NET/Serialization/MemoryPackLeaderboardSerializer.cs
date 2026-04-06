using MemoryPack;

namespace Redisboard.NET.Serialization;

/// <summary>
/// Default <see cref="ILeaderboardSerializer"/> implementation backed by
/// <see cref="MemoryPackSerializer"/> from Cysharp's MemoryPack library.
/// Requires entity types to be annotated with <c>[MemoryPackable]</c> and declared as <c>partial</c>.
/// </summary>
public sealed class MemoryPackLeaderboardSerializer : ILeaderboardSerializer
{
    /// <inheritdoc />
    public byte[] Serialize<T>(T value)
        => MemoryPackSerializer.Serialize(value);

    /// <inheritdoc />
    public T Deserialize<T>(byte[] data)
        => MemoryPackSerializer.Deserialize<T>(data)
           ?? throw new InvalidOperationException($"Deserialization of type '{typeof(T).Name}' returned null.");
}
