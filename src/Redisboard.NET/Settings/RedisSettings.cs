using StackExchange.Redis;

namespace Redisboard.NET.Settings;

public class RedisSettings
{
    public string ConnectionString { get; set; }
    
    public ConfigurationOptions Configuration { get; set; }
}