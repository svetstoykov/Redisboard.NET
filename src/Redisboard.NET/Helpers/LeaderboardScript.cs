using System.Resources;
using StackExchange.Redis;

namespace Redisboard.NET.Helpers;

internal static class LeaderboardScript
{
    private static readonly Lazy<LuaScript> EntityKeysByRangeWithCompetitionRank =
        new(() => LuaScript.Prepare(LoadLuaScript("get_entity_keys_by_range_competition_rank.lua")));

    private static readonly Lazy<LuaScript> EntityKeysByRangeWithDenseRank =
        new(() => LuaScript.Prepare(LoadLuaScript("get_entity_keys_by_range_dense_rank.lua")));

    private static readonly Lazy<LuaScript> UpdateEntityScore =
        new(() => LuaScript.Prepare(LoadLuaScript("update_entity_score.lua")));

    public static LuaScript ForEntityKeysByRangeWithCompetitionRank()
        => EntityKeysByRangeWithCompetitionRank.Value;

    public static LuaScript ForEntityKeysByRangeWithDenseRank()
        => EntityKeysByRangeWithDenseRank.Value;

    public static LuaScript ForUpdateEntityScore()
        => UpdateEntityScore.Value;

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