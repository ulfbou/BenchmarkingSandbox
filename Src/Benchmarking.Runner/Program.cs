
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Running;

using BenchmarkingSandbox.Runner;

public class Program
{
    public static void Main(string[] args)
    {
        var config = ManualConfig.Create(DefaultConfig.Instance)
            .AddExporter(JsonExporter.Full)
            .AddExporter(CsvExporter.Default);
        var summary = BenchmarkRunner.Run<AsyncPriorityQueueBenchmark>(config);
    }
}
