// Copyright (c) Ulf Bourelius. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics.Tracing;

namespace BenchmarkingSandbox.Runner
{
    public class AsyncLockMonitor : EventListener
    {
        private readonly List<(string EventName, int TaskId, DateTime Timestamp)> _events = new();

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == "AsyncLocks")
            {
                EnableEvents(eventSource, EventLevel.Informational);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            _events.Add((eventData.EventName ?? string.Empty, eventData.Payload?[0] as int? ?? -1, DateTime.UtcNow));
        }

        public IEnumerable<(string EventName, int TaskId, DateTime Timestamp)> GetEvents() => _events;

        public void Reset() => _events.Clear();
    }
}
