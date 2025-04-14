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

        public BenchmarkLogger(string category)
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var logDirectory = Path.Combine(AppContext.BaseDirectory, "Logs");

            Directory.CreateDirectory(logDirectory);
            _logFilePath = Path.Combine(logDirectory, $"{category}_{timestamp}.log");

            _writer = new StreamWriter(new FileStream(_logFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                AutoFlush = true
            };

            _writerTask = Task.Run(ProcessQueueAsync);
        }

        public void Log(string category, int taskId, string message)
        {
            var line = $"[{DateTime.UtcNow:O}] [{category}] [Task:{taskId}] {message}";
            _logQueue.Add(line);
        }

        private async Task ProcessQueueAsync()
        {
            foreach (var line in _logQueue.GetConsumingEnumerable(_cts.Token))
            {
                await _writer.WriteLineAsync(line);
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
