using System.Reflection;

namespace Redisboard.NET.Helpers;

internal static class LeaderboardScript
{
    public static string ForEntityKeysByRangeWithCompetitionRank()
        => LoadLuaScript("get_player_ids_by_range_competition_rank.lua");
    
    public static string ForEntityKeysByRangeWithDenseRank()
        => LoadLuaScript("get_player_ids_by_range_dense_rank.lua");

    private static string LoadLuaScript(string scriptName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"Redisboard.NET.Scripts.{scriptName}";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        
        using var reader = new StreamReader(stream);
        
        return reader.ReadToEnd();
    }
}