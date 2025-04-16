// Copyright (c) Ulf Bourelius. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Async.Locks;
using Async.Locks.Events;
using Async.Locks.Monitoring;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;

using BenchmarkingSandbox.Logging;

using System.Runtime.CompilerServices;

namespace BenchmarkingSandbox.Runner
{
    [MemoryDiagnoser]
    [BenchmarkCategory("AsyncLock", "QuickCI")]
    public class AsyncLockBenchmarks
    {
        private static IEnumerable<int> GetConcurrentTasksForCI() => new[] { 1, 3 };
        private static IEnumerable<int> GetConcurrentTasksForNightly() => new[] { 1, 5, 10, 50 };

        private static IEnumerable<int> GetTimeoutMsForCI() => new[] { 0 };
        private static IEnumerable<int> GetCancellationDelayForCI() => new[] { 0 };

        private AsyncLock _asyncLock = null!;
        private AsyncLockMonitor _monitor = null!;
        private BenchmarkLogger _logger = null!;
        private int _resource = 0;
        private const int MaxTaskCount = 10;

        [ParamsSource(nameof(ConcurrentTasksSource))]
        public int ConcurrentTasks { get; set; }

        public IEnumerable<int> ConcurrentTasksSource()
        {
            string environment = Environment.GetEnvironmentVariable("BENCHMARK_PROFILE") ?? "CI";
            return environment.ToUpperInvariant() switch
            {
                "NIGHTLY" => GetConcurrentTasksForNightly(),
                _ => GetConcurrentTasksForCI(),
            };
        }

        [ParamsSource(nameof(TimeoutMsSource))]
        public int TimeoutMs { get; set; }

        public IEnumerable<int> TimeoutMsSource() => Environment.GetEnvironmentVariable("BENCHMARK_PROFILE")?.ToUpperInvariant() == "NIGHTLY" ? new[] { 0, 1, 10 } : GetTimeoutMsForCI();

        [ParamsSource(nameof(CancellationDelaySource))]
        public int CancellationDelay { get; set; }

        public IEnumerable<int> CancellationDelaySource() => Environment.GetEnvironmentVariable("BENCHMARK_PROFILE")?.ToUpperInvariant() == "NIGHTLY" ? new[] { 0, 1, 10 } : GetCancellationDelayForCI();

        [GlobalSetup]
        public void Setup()
        {
            _asyncLock = new AsyncLock(shouldMonitor: true);
            _monitor = new AsyncLockMonitor();
            _monitor.Enable();
            _logger = new BenchmarkLogger(nameof(AsyncLockBenchmarks));
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _monitor?.Dispose();
            _logger?.Dispose();
        }

        [BenchmarkCategory("AcquireRelease", "QuickCI")]
        [Benchmark(Baseline = true)]
        public async Task AcquireRelease_Uncontended()
        {
            await using (await _asyncLock.AcquireAsync())
            {
                await SimulateWorkAsync(1);
            }

            var events = _monitor.GetEvents();
            foreach (var e in events)
            {
                Console.WriteLine($"[AsyncLockMonitor] Task {e.TaskId}: Event={e.EventName}, Timestamp={e.Timestamp}");
            }
            _monitor.Reset();
        }

        [BenchmarkCategory("AcquireRelease", "QuickCI")]
        [Benchmark]
        public async Task AcquireRelease_Contended()
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [AsyncLockMonitor-DEBUG] Entering AcquireRelease_Contended");
            var tasks = new Task[ConcurrentTasks];
            try
            {
                for (int i = 0; i < ConcurrentTasks; i++)
                {
                    var taskLocalId = i;
                    tasks[i] = Task.Run(async () =>
                    {
                        var taskId = Task.CurrentId ?? 0;
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [AsyncLockMonitor-DEBUG] Task {taskId}: Starting AcquireAsync");
                        await using (await _asyncLock.AcquireAsync())
                        {
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [AsyncLockMonitor-DEBUG] Task {taskId}: Lock Acquired");
                            await SimulateWorkAsync(1);
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [AsyncLockMonitor-DEBUG] Task {taskId}: Work Simulated");
                        }
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [AsyncLockMonitor-DEBUG] Task {taskId}: Lock Released");
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
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [AsyncLockMonitor-ERROR] Exception in AcquireRelease_Contended: {ex}");
            }
            finally
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [AsyncLockMonitor-DEBUG] Leaving AcquireRelease_Contended");
            }
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
        private static Task SimulateWorkAsync(int delayMs = 1) => Task.Delay(Math.Min(1, delayMs / 2));
    }
}
