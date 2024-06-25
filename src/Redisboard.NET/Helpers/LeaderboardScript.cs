using System.Reflection;
using System.Resources;

namespace Redisboard.NET.Helpers;

internal static class LeaderboardScript
{
    public static string ForEntityKeysByRangeWithCompetitionRank()
        => LoadLuaScript("get_entity_keys_by_range_competition_rank.lua");
    
    public static string ForEntityKeysByRangeWithDenseRank()
        => LoadLuaScript("get_entity_keys_by_range_dense_rank.lua");

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