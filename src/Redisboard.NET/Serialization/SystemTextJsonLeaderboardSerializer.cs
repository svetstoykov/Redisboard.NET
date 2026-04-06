using System.Text.Json;

namespace Redisboard.NET.Serialization;

/// <summary>
/// Default <see cref="ILeaderboardSerializer"/> implementation backed by
/// <see cref="System.Text.Json.JsonSerializer"/>.
/// </summary>
public sealed class SystemTextJsonLeaderboardSerializer : ILeaderboardSerializer
{
    private readonly JsonSerializerOptions _options;

    /// <summary>
    /// Initializes a new instance using the supplied (or default) <see cref="JsonSerializerOptions"/>.
    /// </summary>
    public SystemTextJsonLeaderboardSerializer(JsonSerializerOptions? options = null)
    {
        _options = options ?? JsonSerializerOptions.Default;
    }

    /// <inheritdoc />
    public string Serialize<T>(T value)
        => JsonSerializer.Serialize(value, _options);

    /// <inheritdoc />
    public T Deserialize<T>(string json)
        => JsonSerializer.Deserialize<T>(json, _options)
           ?? throw new InvalidOperationException($"Deserialization of type '{typeof(T).Name}' returned null.");
}
