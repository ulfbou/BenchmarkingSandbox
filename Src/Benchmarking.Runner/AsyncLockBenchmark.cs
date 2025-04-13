// Copyright (c) Ulf Bourelius. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Async.Locks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;

namespace BenchmarkingSandbox.Runner
{
    [MemoryDiagnoser]
    [ThreadingDiagnoser]
    [RankColumn]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    public class AsyncLockBenchmark
    {
        private AsyncLock _asyncLock = null!;
        private AsyncLock _fifoLock = null!;
        private AsyncLock _lifoLock = null!;
        private AsyncLock _priorityLockConstant = null!;
        private AsyncLock _priorityLockCounterFifo = null!;
        private AsyncLock _priorityLockRandom = null!;
        private AsyncLock _priorityLockHighLow = null!;

        private long _fifoCounter;
        private int _resource = 0;
        private const int DefaultTaskCount = 100;

        [Params(1, 5, 10, 50, 100)]
        public int ConcurrentTasks { get; set; }

        [Params(0, 1, 10, 100)]
        public int InitialDelayMilliseconds { get; set; }

        [Params(1, 10)]
        public int TimeoutMs { get; set; }

        [Params(0, 1, 5)]
        public int LockHoldTimeMs { get; set; } // Varying delay for contention benchmarks

        [GlobalSetup]
        public void Setup()
        {
            _asyncLock = new AsyncLock();
            _fifoLock = new AsyncLock(new FifoLockQueueStrategy());
            _lifoLock = new AsyncLock(new LifoLockQueueStrategy());
            _priorityLockConstant = new AsyncLock(new AsyncPriorityQueueStrategy<int>(tcs => 0));
            _priorityLockCounterFifo = new AsyncLock(new AsyncPriorityQueueStrategy<long>(tcs => Interlocked.Increment(ref _fifoCounter)));
            _priorityLockRandom = new AsyncLock(new AsyncPriorityQueueStrategy<int>(tcs => Random.Shared.Next()));
            var toggle = false;
            _priorityLockHighLow = new AsyncLock(new AsyncPriorityQueueStrategy<int>(tcs => toggle ? 1 : 0));
        }

        [BenchmarkCategory("AcquireRelease")]
        [Benchmark(Baseline = true)]
        public async Task AcquireRelease_Single()
        {
            await using (await _asyncLock.AcquireAsync())
            {
                await Task.Delay(1); // Simulate a very short protected operation
            }
        }

        [BenchmarkCategory("AcquireRelease")]
        [Benchmark]
        public async Task AcquireRelease_Concurrent()
        {
            var tasks = Enumerable.Range(0, ConcurrentTasks)
                .Select(async _ =>
                {
                    await Task.Delay(InitialDelayMilliseconds);
                    await using (await _asyncLock.AcquireAsync())
                    {
                        await Task.Delay(1);
                    }
                }).ToArray();
            await Task.WhenAll(tasks);
        }

        [BenchmarkCategory("AcquireRelease_Fifo")]
        [Benchmark]
        public async Task AcquireRelease_Concurrent_Fifo()
        {
            var tasks = Enumerable.Range(0, ConcurrentTasks)
                .Select(async _ =>
                {
                    await Task.Delay(InitialDelayMilliseconds);
                    await using (await _fifoLock.AcquireAsync())
                    {
                        await Task.Delay(1);
                    }
                }).ToArray();
            await Task.WhenAll(tasks);
        }

        [BenchmarkCategory("AcquireRelease_Lifo")]
        [Benchmark]
        public async Task AcquireRelease_Concurrent_Lifo()
        {
            var tasks = Enumerable.Range(0, ConcurrentTasks)
                .Select(async _ =>
                {
                    await Task.Delay(InitialDelayMilliseconds);
                    await using (await _lifoLock.AcquireAsync())
                    {
                        await Task.Delay(1);
                    }
                }).ToArray();
            await Task.WhenAll(tasks);
        }

        [BenchmarkCategory("TryAcquireTimeout")]
        [Benchmark]
        public async Task TryAcquire_Timeout()
        {
            var timeout = TimeSpan.FromMilliseconds(TimeoutMs);
            await using (await _asyncLock.AcquireAsync(timeout))
            {
                await Task.Delay(1);
            }
        }

        [BenchmarkCategory("TryAcquireTimeout_Contention")]
        [Benchmark]
        public async Task TryAcquire_Timeout_Contention()
        {
            var timeout = TimeSpan.FromMilliseconds(TimeoutMs);
            var tasks = Enumerable.Range(0, ConcurrentTasks)
                .Select(async _ =>
                {
                    await Task.Delay(InitialDelayMilliseconds);
                    try
                    {
                        await using (await _asyncLock.AcquireAsync(timeout))
                        {
                            await Task.Delay(1);
                        }
                    }
                    catch (TimeoutException)
                    {
                        // Expected in some scenarios
                    }
                }).ToArray();
            await Task.WhenAll(tasks);
        }

        [BenchmarkCategory("Cancellation")]
        [Benchmark]
        public async Task Acquire_Cancellation_NoContention()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            try
            {
                await using (await _asyncLock.AcquireAsync(cancellationToken: cts.Token))
                {
                    await Task.Delay(10);
                }
            }
            catch (TaskCanceledException)
            {
                // Expected
            }
        }

        [BenchmarkCategory("Cancellation")]
        [Benchmark]
        public async Task Acquire_Cancellation_Contention()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            var tasks = Enumerable.Range(0, ConcurrentTasks)
                .Select(async _ =>
                {
                    await Task.Delay(InitialDelayMilliseconds);
                    try
                    {
                        await using (await _asyncLock.AcquireAsync(cancellationToken: cts.Token))
                        {
                            await Task.Delay(10);
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        // Expected
                    }
                }).ToArray();
            await Task.WhenAll(tasks);
        }

        [BenchmarkCategory("Contention")]
        [Benchmark]
        public async Task ContendedAcquireRelease()
        {
            var tasks = new Task[ConcurrentTasks]; // Use the parameterized TaskCount
            for (int i = 0; i < ConcurrentTasks; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    await using (await _asyncLock.AcquireAsync())
                    {
                        Interlocked.Increment(ref _resource);
                        await Task.Delay(LockHoldTimeMs); // Simulate some work using the parameterized delay
                        Interlocked.Decrement(ref _resource);
                    }
                });
            }
            await Task.WhenAll(tasks);
        }

        [BenchmarkCategory("Uncontended")]
        [Benchmark]
        public async Task UncontendedAcquireRelease()
        {
            await using (await _asyncLock.AcquireAsync())
            {
                await using (await _asyncLock.AcquireAsync())
                {
                    Interlocked.Increment(ref _resource);
                    await Task.Delay(1);
                    Interlocked.Decrement(ref _resource);
                }
            }
        }

        [BenchmarkCategory("Contention_Strategies")]
        [Benchmark(Baseline = true)]
        public async Task FifoContention() => await RunContentionBenchmark(_fifoLock);

        [BenchmarkCategory("Contention_Strategies")]
        [Benchmark]
        public async Task PriorityContentionConstant() => await RunContentionBenchmark(_priorityLockConstant);

        [BenchmarkCategory("Contention_Strategies")]
        [Benchmark]
        public async Task PriorityContentionCounterFifo() => await RunContentionBenchmark(_priorityLockCounterFifo);

        [BenchmarkCategory("Contention_Strategies")]
        [Benchmark]
        public async Task PriorityContentionRandom() => await RunContentionBenchmark(_priorityLockRandom);

        [BenchmarkCategory("Contention_Strategies")]
        [Benchmark]
        public async Task PriorityContentionHighLow() => await RunContentionBenchmark(_priorityLockHighLow);

        private async Task RunContentionBenchmark(AsyncLock asyncLock)
        {
            var tasks = new Task[ConcurrentTasks]; // Use the parameterized TaskCount
            for (int i = 0; i < ConcurrentTasks; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    await using (await asyncLock.AcquireAsync())
                    {
                        await Task.Delay(LockHoldTimeMs); // Use the parameterized LockHoldTimeMs
                    }
                });
            }
            await Task.WhenAll(tasks);
        }

        [GlobalCleanup]
        public Task Cleanup()
        {
            _asyncLock?.DisposeAsync();
            _fifoLock?.DisposeAsync();
            _lifoLock?.DisposeAsync();
            _priorityLockConstant?.DisposeAsync();
            _priorityLockCounterFifo?.DisposeAsync();
            _priorityLockRandom?.DisposeAsync();
            _priorityLockHighLow?.DisposeAsync();
            return Task.CompletedTask;
        }
    }
}
