using Async.Collections;

using BenchmarkDotNet.Attributes;

namespace BenchmarkingSandbox.Runner
{
    [MemoryDiagnoser]
    [Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.Declared)]
    [RankColumn]
    public class AsyncPriorityQueueBenchmark
    {
        private AsyncPriorityQueue<int, int> _queue = default!;
        private int[] _dataToAdd = default!;

        [Params(100, 1000, 10000)]
        public int N { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _queue = new AsyncPriorityQueue<int, int>(i => i);
            _dataToAdd = Enumerable.Range(0, N).ToArray();
        }

        [Benchmark]
        public async Task AddMultipleAsync()
        {
            foreach (var item in _dataToAdd)
            {
                await _queue.AddAsync(item);
            }
        }

        [Benchmark]
        public async Task ContainsMultipleAsync()
        {
            foreach (var item in _dataToAdd)
            {
                await _queue.ContainsAsync(item);
            }
        }

        [Benchmark]
        public async Task CountMultipleAsync()
        {
            for (int i = 0; i < N; i++)
            {
                await _queue.CountAsync();
            }
        }

        [Benchmark]
        public async Task RemoveMultipleAsync()
        {
            foreach (var item in _dataToAdd)
            {
                await _queue.RemoveAsync(item);
            }
        }

        [Benchmark]
        public async Task ToArrayAsync()
        {
            await _queue.ToArrayAsync();
        }

        [Benchmark]
        public async Task GetAsyncEnumeratorMultiple()
        {
            await foreach (var _ in _queue)
            {
                // Consume the items
            }
        }

        [GlobalCleanup]
        public async Task Cleanup()
        {
            await _queue.DisposeAsync();
        }
    }
}
