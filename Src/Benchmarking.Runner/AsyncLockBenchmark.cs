// Copyright (c) FluentInjections Project. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Async.Locks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;

namespace BenchmarkingSandbox.Runner
{
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.Declared)]
    [RankColumn]
    public class AsyncLockBenchmark
    {
        private AsyncLock _asyncLock = null!;
        private AsyncLock _fifoLock = null!;
        private AsyncLock _lifoLock = null!;

        [Params(1, 5, 10, 50, 100)]
        public int ConcurrentTasks { get; set; }

        [Params(0, 1, 10, 100)]
        public int InitialDelayMilliseconds { get; set; }

        [Params(1, 10)]
        public int TimeoutMs { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _asyncLock = new AsyncLock();
            _fifoLock = new AsyncLock(new FifoLockQueueStrategy());
            _lifoLock = new AsyncLock(new LifoLockQueueStrategy());
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
    }
}