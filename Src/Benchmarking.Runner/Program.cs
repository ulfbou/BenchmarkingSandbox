// Copyright (c) Ulf Bourelius. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Running;

using Benchmarking.Runners;

using BenchmarkingSandbox.Runner;

namespace Benchmarking.Runner
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var config = ManualConfig.Create(DefaultConfig.Instance)
                .AddExporter(JsonExporter.Full)
                .AddExporter(CsvExporter.Default);
            var priorityQueueSummary = BenchmarkRunner.Run<AsyncPriorityQueueBenchmarks>();
            var lockSummary = BenchmarkRunner.Run<AsyncLockBenchmark>();
        }
    }
}
