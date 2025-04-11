// BenchmarkingSandbox/src/Benchmarking.Runner/Program.cs

using BenchmarkDotNet.Running;

namespace BenchmarkingSandbox.Runner
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<SimpleBenchmark>();
            var asyncQueueSummary = BenchmarkRunner.Run<AsyncPriorityQueueBenchmark>();
        }
    }
}
