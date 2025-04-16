// Copyright (c) Ulf Bourelius. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

using BenchmarkingSandbox.Logging;
using BenchmarkingSandbox.Runner;

using Perfolizer.Horology;

namespace BenchmarkingSandbox.Runner
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var timeoutMinutes = TryParseTimeoutArg(args, defaultMinutes: 15);
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(timeoutMinutes));
            var token = cts.Token;

            var shortJob = Job.Dry
                .WithIterationCount(3)
                .WithWarmupCount(1)
                .WithInvocationCount(1)
                .WithUnrollFactor(1)
                .WithMinIterationTime(TimeInterval.FromMilliseconds(50));

            var config = ManualConfig.Create(DefaultConfig.Instance)
                .AddJob(shortJob)
                .AddExporter(JsonExporter.Full)
                .AddExporter(CsvExporter.Default)
                .AddColumn(StatisticColumn.Min)
                .AddColumn(StatisticColumn.Max)
                .AddColumn(StatisticColumn.Mean)
                .AddDiagnoser(ThreadingDiagnoser.Default)
                .AddDiagnoser(MemoryDiagnoser.Default);

            Console.WriteLine($"{DateTime.Now}: Starting benchmarks with a timeout of {timeoutMinutes} minute(s).");

            try
            {
                Task.WaitAll(new[]
                {
                    Task.Run(() => BenchmarkRunner.Run<AsyncPriorityQueueBenchmarks>(config), token)
                    //Task.Run(() => BenchmarkRunner.Run<AsyncLockBenchmark>(config), token)
                }, token);
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine($"Benchmark execution cancelled after timeout of {timeoutMinutes} minute(s).");
            }
            finally
            {
                Console.WriteLine($"{DateTime.Now}: All benchmarks completed.");
            }
        }

        private static int TryParseTimeoutArg(string[] args, int defaultMinutes)
        {
            if (args.Length > 0 && int.TryParse(args[0], out var parsed) && parsed > 0)
                return parsed;

            return defaultMinutes;
        }
    }
}
