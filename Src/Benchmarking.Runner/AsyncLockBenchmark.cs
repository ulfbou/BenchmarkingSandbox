// Copyright (c) Ulf Bourelius. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Async.Locks;
using Async.Locks.Events;
using Async.Locks.Monitoring;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Diagnosers;

namespace BenchmarkingSandbox.Runner
{
    [MemoryDiagnoser]
    [ThreadingDiagnoser]
    [RankColumn]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [WarmupCount(10)]
    [IterationCount(20)]
    [InvocationCount(8)]
    public class AsyncLockBenchmark
    {
        private AsyncLock _asyncLock = null!;
        private AsyncLockMonitor _monitor = null!;
        private int _resource = 0;
        private const int MaxTaskCount = 100;

        [Params(1, 5, 10, 50, MaxTaskCount)]
        public int ConcurrentTasks { get; set; }

        [Params(0, 1, 10, 50)]
        public int TimeoutMs { get; set; }

        [Params(0, 1, 10, 50)]
        public int CancellationDelay { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _asyncLock = new AsyncLock(shouldMonitor: true);
            _monitor = new AsyncLockMonitor();
            _monitor.Enable();
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _monitor?.Dispose();
        }

        [BenchmarkCategory("AcquireRelease")]
        [Benchmark(Baseline = true)]
        public async Task AcquireRelease_Uncontended()
        {
            await using (await _asyncLock.AcquireAsync())
            {
                await Task.Delay(1);
            }
        }

        [BenchmarkCategory("AcquireRelease")]
        [Benchmark]
        public async Task AcquireRelease_Contended()
        {
            var tasks = new Task[ConcurrentTasks];
            for (int i = 0; i < ConcurrentTasks; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    var taskId = Task.CurrentId ?? 0;

                    AsyncLockEvents.Log.TaskStarted(taskId);
                    await using (await _asyncLock.AcquireAsync())
                    {
                        AsyncLockEvents.Log.LockAcquired(taskId);
                        await Task.Delay(1);
                        AsyncLockEvents.Log.LockReleased(taskId);
                    }
                    AsyncLockEvents.Log.TaskCompleted(taskId);
                });
            }
            await Task.WhenAll(tasks);

            var events = _monitor.GetEvents();

            foreach (var e in events)
            {
                Console.WriteLine($"Event: {e.EventName}, TaskId: {e.TaskId}, Timestamp: {e.Timestamp}");
            }

            _monitor.Reset();
        }

        [BenchmarkCategory("Contention")]
        [Benchmark]
        public async Task ContendedIncrementDecrement()
        {
            var tasks = new Task[ConcurrentTasks];
            for (int i = 0; i < ConcurrentTasks; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    await using (await _asyncLock.AcquireAsync())
                    {
                        Interlocked.Increment(ref _resource);
                        await Task.Delay(1);
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
                    await Task.Delay(1);
                }
            }
        }

        [BenchmarkCategory("TryAcquireTimeout")]
        [Benchmark]
        public async Task TryAcquire_Timeout_Successful()
        {
            await using (await _asyncLock.AcquireAsync(TimeSpan.FromMilliseconds(TimeoutMs > 0 ? TimeoutMs : Timeout.Infinite)))
            {
                await Task.Delay(1);
            }
        }

        [BenchmarkCategory("TryAcquireTimeout_Contention")]
        [Benchmark]
        public async Task TryAcquire_Timeout_Failure_Contended()
        {
            var timeout = TimeSpan.FromMilliseconds(TimeoutMs);
            var tasks = Enumerable.Range(0, ConcurrentTasks)
                .Select(async _ =>
                {
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
            using var cts = new CancellationTokenSource(CancellationDelay);
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
            using var cts = new CancellationTokenSource(CancellationDelay);
            var tasks = new Task[ConcurrentTasks];
            var cancelledCount = 0;

            for (int i = 0; i < ConcurrentTasks; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    try
                    {
                        await using (await _asyncLock.AcquireAsync(cancellationToken: cts.Token))
                        {
                            await Task.Delay(10);
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        Interlocked.Increment(ref cancelledCount);
                    }
                    catch (TimeoutException)
                    {
                        Console.WriteLine($"Task {Task.CurrentId} timed out.");
                        Interlocked.Increment(ref cancelledCount);
                    }
                });
            }
            await Task.WhenAll(tasks);

            if (cancelledCount > 0)
            {
                Console.WriteLine($"Cancelled tasks: {cancelledCount}");
            }
            else
            {
                Console.WriteLine("No tasks were cancelled.");
            }
        }
    }
}
