using System.Resources;
using StackExchange.Redis;

namespace Redisboard.NET.Helpers;

/// <summary>
/// Provides pre-compiled Lua scripts for leaderboard operations.
/// All scripts are loaded lazily from embedded resources on first access.
/// </summary>
internal static class LeaderboardScript
{
    /// <summary>Loads a single-snapshot entity read.</summary>
    /// <remarks>
    /// KEYS: leaderboard, unique scores, metadata. ARGV: query (rank, score, neighbors), ranking type,
    /// then inclusive zero-based rank bounds, inverted score bounds, or member and neighbor offset.
    /// Returns rows of [binary member, rank, exact score string, binary metadata or false].
    /// </remarks>
    private static readonly Lazy<LuaScript> Entities =
        new(() => LuaScript.Prepare(LoadLuaScript("get_entities.lua")));

    /// <summary>Loads a rank lookup that does not access metadata.</summary>
    /// <remarks>
    /// KEYS: leaderboard and unique scores. ARGV: member and ranking type (2, 3, or 4).
    /// Returns the one-based rank, or false when the member does not exist.
    /// </remarks>
    private static readonly Lazy<LuaScript> EntityRank =
        new(() => LuaScript.Prepare(LoadLuaScript("get_entity_rank.lua")));

    /// <summary>
    /// Updates an entity's score in the leaderboard and maintains the unique scores set.
    /// KEYS[1]: Sorted set cache key (leaderboard).
    /// KEYS[2]: Unique scores sorted set cache key.
    /// ARGV[1]: Member identifier.
    /// ARGV[2]: New score.
    /// Returns the new rank in the unique scores set (1-based).
    /// </summary>
    private static readonly Lazy<LuaScript> UpdateEntityScore =
        new(() => LuaScript.Prepare(LoadLuaScript("update_entity_score.lua")));

    /// <summary>
    /// Adds multiple entities to the leaderboard in a single atomic operation.
    /// KEYS[1]: Leaderboard sorted set key.
    /// KEYS[2]: Unique scores sorted set key.
    /// KEYS[3]: Metadata hash key.
    /// ARGV: Triplets of [entityKey, invertedScore, metadata].
    /// Returns 1 on success.
    /// </summary>
    private static readonly Lazy<LuaScript> AddEntitiesBatch =
        new(() => LuaScript.Prepare(LoadLuaScript("add_entities_batch.lua")));

    /// <summary>
    /// Deletes multiple entities from the leaderboard in a single atomic operation.
    /// KEYS[1]: Leaderboard sorted set key.
    /// KEYS[2]: Unique scores sorted set key.
    /// KEYS[3]: Metadata hash key.
    /// ARGV: Entity keys to delete.
    /// Returns 1 on success.
    /// </summary>
    private static readonly Lazy<LuaScript> DeleteEntitiesBatch =
        new(() => LuaScript.Prepare(LoadLuaScript("delete_entities_batch.lua")));

    /// <summary>Gets the prepared script for atomic entity reads.</summary>
    /// <returns>A read-only script that returns ranking data and metadata together.</returns>
    public static LuaScript ForEntities() => Entities.Value;

    /// <summary>Gets the prepared script for atomic rank-only reads.</summary>
    /// <returns>A read-only script that returns a rank without fetching metadata.</returns>
    public static LuaScript ForEntityRank() => EntityRank.Value;

    /// <summary>
    /// Gets the pre-compiled Lua script for updating an entity's score.
    /// </summary>
    /// <returns>The prepared Lua script.</returns>
    public static LuaScript ForUpdateEntityScore()
        => UpdateEntityScore.Value;

    /// <summary>
    /// Gets the pre-compiled Lua script for batch adding entities.
    /// </summary>
    /// <returns>The prepared Lua script.</returns>
    public static LuaScript ForAddEntitiesBatch()
        => AddEntitiesBatch.Value;

    /// <summary>
    /// Gets the pre-compiled Lua script for batch deleting entities.
    /// </summary>
    /// <returns>The prepared Lua script.</returns>
    public static LuaScript ForDeleteEntitiesBatch()
        => DeleteEntitiesBatch.Value;

    /// <summary>
    /// Loads a Lua script from embedded resources.
    /// </summary>
    /// <param name="scriptName">Name of the script file.</param>
    /// <returns>The script content as a string.</returns>
    /// <exception cref="MissingManifestResourceException">Thrown when the script resource is not found.</exception>
    private static string LoadLuaScript(string scriptName)
    {
        var assembly = typeof(LeaderboardScript).Assembly;
        var resourceName = $"Redisboard.NET.Scripts.{scriptName}";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        
        if (stream == null)
        {
            throw new MissingManifestResourceException(
                $"Resource '{resourceName}' not found in assembly '{assembly.FullName}'.");
        }
        
        using var reader = new StreamReader(stream);
        
        return reader.ReadToEnd();
    }
}
