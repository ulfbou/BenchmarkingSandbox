using BenchmarkingSandbox.Core;

namespace BenchmarkingSandbox.Runner
{
    public class NoOpAlgorithm : IAlgorithm
    {
        public string Name => "NoOp";
        public void Execute(object input)
        {
            // Perform no operation
        }
    }
}
