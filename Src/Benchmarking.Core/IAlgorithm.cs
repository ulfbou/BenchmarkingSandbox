namespace BenchmarkingSandbox.Core
{
    public interface IAlgorithm
    {
        string Name { get; }
        void Execute(object input);
    }
}
