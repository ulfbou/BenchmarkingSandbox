using Async.Collections;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace Benchmarking.Runners
{
    [MemoryDiagnoser]
    public class AsyncPriorityQueueBenchmarks
    {
        private AsyncPriorityQueue<int, int> _priorityQueue = null!;
        private List<int> _itemsToAdd = null!;
        private List<int> _itemsToContain = null!;
        private List<int> _itemsToRemove = null!;

        [Params(100, 1000, 10000)]
        public int N { get; set; }

        [GlobalSetup]
        public void GlobalSetup()
        {
            _priorityQueue = new AsyncPriorityQueue<int, int>(i => i);
            _itemsToAdd = Enumerable.Range(0, N).ToList();
            _itemsToContain = Enumerable.Range(N / 4, N / 2).ToList();
            _itemsToRemove = Enumerable.Range(0, N / 3).ToList();

            foreach (var item in _itemsToAdd)
            {
                _priorityQueue.AddAsync(item).AsTask().Wait();
            }
        }

        [IterationCleanup]
        public async Task IterationCleanup()
        {
            await _priorityQueue.ClearAsync();

            foreach (var item in _itemsToAdd)
            {
                await _priorityQueue.AddAsync(item);
            }
        }

        [Benchmark]
        public async Task AddMultipleAsync()
        {
            var itemsToAdd = Enumerable.Range(N, N).ToList();
            await _priorityQueue.AddMultipleAsync(itemsToAdd);
        }

        [Benchmark]
        public async Task ContainsMultipleAsync()
        {
            await _priorityQueue.ContainsMultipleAsync(_itemsToContain);
        }

        [Benchmark]
        public async Task CountMultipleAsync()
        {
            await _priorityQueue.CountMultipleAsync(_itemsToContain);
        }

        [Benchmark]
        public async Task RemoveMultipleAsync()
        {
            await _priorityQueue.RemoveMultipleAsync(_itemsToRemove);
        }
    }
}
