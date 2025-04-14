// Copyright (c) Ulf Bourelius. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Async.Collections;

using BenchmarkDotNet.Attributes;

using Benchmarking.Runners;

using BenchmarkingSandbox.Logging;

namespace BenchmarkingSandbox.Runner
{
    [BenchmarkCategory("AsyncPriorityQueue")]
    public class AsyncPriorityQueueBenchmarks
    {
        private AsyncPriorityQueue<int, int> _priorityQueue = null!;
        private List<int> _itemsToAdd = null!;
        private List<int> _itemsToContain = null!;
        private List<int> _itemsToRemove = null!;
        private BenchmarkLogger _logger = null!;

        [Params(100, 1000)]
        public int N { get; set; }

        [GlobalSetup]
        public void GlobalSetup()
        {
            _priorityQueue = new AsyncPriorityQueue<int, int>(i => i);
            _itemsToAdd = new List<int>(N);
            _itemsToContain = new List<int>(N / 2);
            _itemsToRemove = new List<int>(N / 3);

            for (int i = 0; i < N; i++)
            {
                _itemsToAdd.Add(i);
                if (i >= N / 4 && i < N / 4 + N / 2)
                {
                    _itemsToContain.Add(i);
                }

                if (i < N / 3)
                {
                    _itemsToRemove.Add(i);
                }
            }

            _logger = new BenchmarkLogger(nameof(AsyncPriorityQueueBenchmarks));
            _logger.Log("Setup", -1, $"Initialized with N={N}");

            foreach (var item in _itemsToAdd)
            {
                _priorityQueue.AddAsync(item).AsTask().Wait();
            }

            _logger.Log("Setup", -1, $"Prepopulated queue with {_itemsToAdd.Count} items");
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            _logger?.Dispose();
        }

        [IterationCleanup]
        public void IterationCleanup()
        {
            if (_itemsToAdd.Count <= 1000)
            {
                _priorityQueue.AddMultipleAsync(_itemsToAdd).Wait();
            }

            _priorityQueue.ClearAsync().AsTask().Wait();
            _logger.Log("Cleanup", -1, $"Reset queue after iteration with {_itemsToAdd.Count} items");
        }

        // Focused benchmark: Add items only for the prototype phase
        [Benchmark]
        [BenchmarkCategory("Add")]
        public async Task AddMultipleAsync()
        {
            var itemsToAdd = Enumerable.Range(N, N).ToList();
            await _priorityQueue.AddMultipleAsync(itemsToAdd);

            _logger.Log("AddMultipleAsync", Task.CurrentId ?? -1, $"Added {itemsToAdd.Count} items");
        }

        //[Benchmark]
        //[BenchmarkCategory("Contains")]
        //public async Task ContainsMultipleAsync()
        //{
        //    await _priorityQueue.ContainsMultipleAsync(_itemsToContain);

        //    _logger.Log("ContainsMultipleAsync", Task.CurrentId ?? -1, $"Checked {_itemsToContain.Count} items");
        //}

        //[Benchmark]
        //[BenchmarkCategory("Count")]
        //public async Task CountMultipleAsync()
        //{
        //    await _priorityQueue.CountMultipleAsync(_itemsToContain);

        //    _logger.Log("CountMultipleAsync", Task.CurrentId ?? -1, $"Counted {_itemsToContain.Count} items");
        //}

        //[Benchmark]
        //[BenchmarkCategory("Remove")]
        //public async Task RemoveMultipleAsync()
        //{
        //    await _priorityQueue.RemoveMultipleAsync(_itemsToRemove);

        //    _logger.Log("RemoveMultipleAsync", Task.CurrentId ?? -1, $"Removed {_itemsToRemove.Count} items");
        //}
    }
}
