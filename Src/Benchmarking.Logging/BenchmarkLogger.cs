// Copyright (c) Ulf Bourelius. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;

namespace BenchmarkingSandbox.Logging
{
    /// <summary>
    /// A logger for benchmarking tasks that writes logs to a file.
    /// </summary>
    public sealed class BenchmarkLogger : IDisposable
    {
        private readonly BlockingCollection<string> _logQueue = new();
        private readonly Task _writerTask;
        private readonly StreamWriter _writer;

        private readonly CancellationTokenSource _cts = new();
        private readonly string _logFilePath;

        /// <summary>
        /// Initializes a new instance of the <see cref="BenchmarkLogger"/> class.
        /// </summary>
        /// <param name="category">The category of the benchmark.</param>
        /// <param name="rootLogPath">The root path for log files. If null, defaults to the current directory.</param>
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

        /// <summary>
        /// Logs a message with the specified category and task ID.
        /// </summary>
        /// <param name="category">The category of the log message.</param>
        /// <param name="taskId">The ID of the task.</param>
        /// <param name="message">The log message.</param>
        public void Log(string category, int taskId, string message)
        {
            var line = $"[{DateTime.UtcNow:O}] [{category}] [Task:{taskId}] {message}";
            _logQueue.Add(line);
        }

        /// <inheritdoc />
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
    }
}
