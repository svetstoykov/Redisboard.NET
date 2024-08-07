﻿using System.Text.Json;
using Redisboard.NET.Interfaces;
using StackExchange.Redis;

namespace Redisboard.NET.Common.Models;

public class Player : ILeaderboardEntity
{
    public RedisValue Key { get; set; }

    public long Rank { get; set; }

    public double Score { get; set; }
    public RedisValue Metadata { get; set; }

    public static Player New()
    {
        var random = new Random();

        return new Player
        {
            Key = Guid.NewGuid().ToString(),
            Score = random.Next(),
            Metadata = JsonSerializer.Serialize(new PlayerData()
            {
                EntryDate = DateTime.Now,
                Username = $"user_{random.Next()}",
                FirstName = $"first_{random.Next()}",
                LastName = $"last_{random.Next()}"
            })
        };
    }
}

public class PlayerData
{
    public string Username { get; set; }

    public string FirstName { get; set; }

    public string LastName { get; set; }

    public DateTime EntryDate { get; set; }
}