using BenchmarkDotNet.Attributes;

namespace BenchmarkingSandbox.Runner
{
    public class SimpleBenchmark
    {
        private readonly NoOpAlgorithm _algorithm = new NoOpAlgorithm();

        [Params(100)]
        public int N { get; set; }

        [Benchmark]
        public void RunNoOpAlgorithm()
        {
            _algorithm.Execute(N);
        }
    }
}
