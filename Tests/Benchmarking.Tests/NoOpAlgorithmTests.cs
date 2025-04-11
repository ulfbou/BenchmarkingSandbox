using BenchmarkingSandbox.Core;
using BenchmarkingSandbox.Runner;
using Xunit;

namespace BenchmarkingSandbox.Tests
{
    public class NoOpAlgorithmTests
    {
        [Fact]
        public void NoOpAlgorithm_ExecutesWithoutError()
        {
            IAlgorithm algorithm = new NoOpAlgorithm();
            algorithm.Execute(10);
            Assert.True(true);
        }
    }
}
