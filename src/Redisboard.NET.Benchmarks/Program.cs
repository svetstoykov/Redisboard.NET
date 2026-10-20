using BenchmarkDotNet.Running;
using Redisboard.NET.Benchmarks;

BenchmarkSwitcher.FromAssembly(typeof(LeaderboardReadBenchmarks).Assembly).Run(args);
