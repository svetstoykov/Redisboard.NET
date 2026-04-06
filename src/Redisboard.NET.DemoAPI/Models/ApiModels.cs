namespace Redisboard.NET.DemoAPI.Models;

public class AddPlayerRequest
{
    public string PlayerId { get; set; }
    public string Metadata { get; set; }
}

public class UpdateScoreRequest
{
    public double NewScore { get; set; }
}

public class UpdateMetadataRequest
{
    public string Metadata { get; set; }
}

public class EntityResponse
{
    public string Key { get; set; }
    public double? Score { get; set; }
    public long? Rank { get; set; }
    public string Metadata { get; set; }
}

public class SizeResponse
{
    public long Size { get; set; }
}