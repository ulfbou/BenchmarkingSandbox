// Copyright (c) Ulf Bourelius. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;

namespace BenchmarkingSandbox.Logging
{
    public sealed class BenchmarkLogger : IDisposable
    {
        private readonly BlockingCollection<string> _logQueue = new();
        private readonly Task _writerTask;
        private readonly StreamWriter _writer;

        private readonly CancellationTokenSource _cts = new();
        private readonly string _logFilePath;

        public BenchmarkLogger(string category, string? rootLogPath = null)
        {
            _logFilePath = Initialize(category, rootLogPath);

            _writer = new StreamWriter(new FileStream(_logFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                AutoFlush = true
            };

            Console.WriteLine($"[BenchmarkLogger] Logging to {_logFilePath}");
            _writerTask = Task.Run(ProcessQueueAsync);
        }

        private string Initialize(string category, string? rootLogPath)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                throw new ArgumentException("Category cannot be empty or whitespace.", nameof(category));
            }

            string baseLogDirectory;

            if (!string.IsNullOrEmpty(rootLogPath))
            {
                baseLogDirectory = Path.Combine(rootLogPath, "benchmark-logs");
            }
            else
            {
                baseLogDirectory = Path.Combine(Environment.ExpandEnvironmentVariables("GITHUB_WORKSPACE") ?? AppContext.BaseDirectory, "Logs", "benchmark-logs");
            }

            Directory.CreateDirectory(baseLogDirectory);

            var logFileName = $"{category}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.log";
            return Path.Combine(baseLogDirectory, logFileName);
        }

        public void Log(string category, int taskId, string message)
        {
            var line = $"[{DateTime.UtcNow:O}] [{category}] [Task:{taskId}] {message}";
            _logQueue.Add(line);
        }

        private async Task ProcessQueueAsync()
        {
            try
            {
                foreach (var line in _logQueue.GetConsumingEnumerable(_cts.Token))
                {
                    await _writer.WriteLineAsync(line);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when _cts.Cancel() is called
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BenchmarkLogger Error] Exception during log writing: {ex}");
            }
            finally
            {
                await _writer.FlushAsync();
            }
        }

        public void Dispose()
        {
            _logQueue.CompleteAdding();
            _cts.Cancel();
            try
            {
                _writerTask.Wait();
            }
            catch { /* ignore */ }

            _writer.Dispose();
            _cts.Dispose();
        }
    }
}
