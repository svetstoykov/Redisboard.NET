using Redisboard.NET.DemoAPI;
using Redisboard.NET.DemoAPI.Models;
using Redisboard.NET.DemoAPI.Settings;
using Redisboard.NET.Interfaces;
using Redisboard.NET.IoC;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var redisSettings = builder.Configuration
    .GetSection(nameof(DemoRedisSettings))
    .Get<DemoRedisSettings>();

builder.Services.AddLeaderboardManager<Player>(redisSettings);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

const int leaderboardId = 1;

var random = new Random();
var playersToAdd = new List<Player>();
for (int i = 0; i < 2; i++)
{
    for (int j = 0; j < 2; j++)
    {
        var generated = new Player()
        {
            Id = Guid.NewGuid().ToString(),
            EntryDate = DateTime.Now,
            FirstName = $"FirstName_{i}_{j}",
            LastName = $"LastName_{i}_{j}",
            Score = random.Next(1, 75000),
            Username = $"user_{i}_j"
        };
        
        playersToAdd.Add(generated);
    }
}

var scope = app.Services.CreateScope();
var manager = scope.ServiceProvider.GetRequiredService<ILeaderboardManager<Player>>();

app.MapPost("/", async (Player[] player, ILeaderboardManager<Player> leaderboardManager)
    => await leaderboardManager.AddToLeaderboardAsync(leaderboardId, player));

app.MapGet("/{id}", async (string id, ILeaderboardManager<Player> leaderboardManager)
    => await leaderboardManager.GetEntityAndNeighboursByIdAsync(leaderboardId, id));

app.Run();