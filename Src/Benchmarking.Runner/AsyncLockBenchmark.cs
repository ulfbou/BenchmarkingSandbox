// Copyright (c) Ulf Bourelius. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Async.Locks;
using Async.Locks.Events;
using Async.Locks.Monitoring;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

using BenchmarkingSandbox.Logging;
using System.Runtime.CompilerServices;

namespace BenchmarkingSandbox.Runner
{
    [MemoryDiagnoser]
    [MinIterationTime(200)]
    [ThreadingDiagnoser]
    [RankColumn]
    [CategoriesColumn]
    [BenchmarkCategory("AsyncLock")]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [HideColumns("Error", "StdDev", "Median")]
    [MarkdownExporter, HtmlExporter, CsvExporter, JsonExporter]
    public class AsyncLockBenchmark
    {
        private AsyncLock _asyncLock = null!;
        private AsyncLockMonitor _monitor = null!;
        private BenchmarkLogger _logger = null!;
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
            _logger = new BenchmarkLogger(nameof(AsyncLockBenchmark));
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _monitor?.Dispose();
            _logger?.Dispose();
        }

        [BenchmarkCategory("AcquireRelease")]
        [Benchmark(Baseline = true)]
        public async Task AcquireRelease_Uncontended()
        {
            await using (await _asyncLock.AcquireAsync())
            {
                await SimulateWorkAsync(1);
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
                        await SimulateWorkAsync(1);
                        AsyncLockEvents.Log.LockReleased(taskId);
                    }
                    AsyncLockEvents.Log.TaskCompleted(taskId);
                });
            }
            await Task.WhenAll(tasks);

            var events = _monitor.GetEvents();
            foreach (var e in events)
            {
                _logger.Log("AsyncLockMonitor", e.TaskId, $"Event: {e.EventName}, Timestamp: {e.Timestamp}");
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
                        await SimulateWorkAsync(1);
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
                    await SimulateWorkAsync(1);
                }
            }
        }

        [BenchmarkCategory("TryAcquireTimeout")]
        [Benchmark]
        public async Task TryAcquire_Timeout_Successful()
        {
            await using (await _asyncLock.AcquireAsync(TimeSpan.FromMilliseconds(TimeoutMs > 0 ? TimeoutMs : Timeout.Infinite)))
            {
                await SimulateWorkAsync(1);
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
                            await SimulateWorkAsync(1);
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
                    await SimulateWorkAsync(10);
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
                            await SimulateWorkAsync(10);
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        Interlocked.Increment(ref cancelledCount);
                    }
                    catch (TimeoutException)
                    {
                        _logger.Log("AsyncLockMonitor", Task.CurrentId ?? 0, $"Task {Task.CurrentId} timed out.");
                        Interlocked.Increment(ref cancelledCount);
                    }
                });
            }
            await Task.WhenAll(tasks);

            _logger.Log("AsyncLockMonitor", 0, $"Cancelled tasks: {cancelledCount}");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Task SimulateWorkAsync(int delayMs = 1) => Task.Delay(delayMs);
    }
}
