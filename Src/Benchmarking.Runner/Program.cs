// Copyright (c) Ulf Bourelius. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

using BenchmarkingSandbox.Runner;

using Perfolizer.Horology;

namespace Benchmarking.Runner
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var config = ManualConfig.Create(DefaultConfig.Instance)
                .AddJob(Job.ShortRun.WithMinIterationTime(TimeInterval.FromMilliseconds(50)))
                .AddExporter(JsonExporter.Full)
                .AddExporter(CsvExporter.Default)
                .AddColumn(StatisticColumn.Min)
                .AddColumn(StatisticColumn.Max)
                .AddColumn(StatisticColumn.Mean)
                .AddDiagnoser(ThreadingDiagnoser.Default)
                .AddDiagnoser(MemoryDiagnoser.Default);

            var priorityQueueSummary = BenchmarkRunner.Run<AsyncPriorityQueueBenchmarks>(config);
            var lockSummary = BenchmarkRunner.Run<AsyncLockBenchmark>(config);
        }
    }
}
