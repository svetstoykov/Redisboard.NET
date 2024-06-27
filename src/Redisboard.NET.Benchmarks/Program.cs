
using BenchmarkDotNet.Running;
using Redisboard.NET.Benchmarks;
using Redisboard.NET.Benchmarks.Helpers;

await BenchmarkLeaderboardHelper.InitializeBenchmarksLeaderboardAsync();

BenchmarkRunner.Run<GetEntityAndNeighboursBenchmarks>();