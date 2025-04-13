// Copyright (c) Ulf Bourelius. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Async.Collections;

namespace Benchmarking.Runners
{
    public static class AsyncPriorityQueueExtensions
    {
        public static async Task AddMultipleAsync<TPriority, TItem>(this AsyncPriorityQueue<TPriority, TItem> queue, IEnumerable<TItem> items)
            where TPriority : IComparable<TPriority>
        {
            foreach (var item in items)
            {
                await queue.AddAsync(item);
            }
        }

        public static async Task<List<bool>> ContainsMultipleAsync<TPriority, TItem>(this AsyncPriorityQueue<TPriority, TItem> queue, IEnumerable<TItem> items)
            where TPriority : IComparable<TPriority>
        {
            var results = new List<bool>();
            foreach (var item in items)
            {
                results.Add(await queue.ContainsAsync(item));
            }
            return results;
        }

        public static async Task<List<int>> CountMultipleAsync<TPriority, TItem>(this AsyncPriorityQueue<TPriority, TItem> queue, IEnumerable<TItem> items)
            where TPriority : IComparable<TPriority>
        {
            var results = new List<int>();
            foreach (var item in items)
            {
                // While not a direct "multiple count" operation in the traditional sense,
                // this benchmark helps understand the overhead of repeated individual CountAsync calls.
                // If a true "CountMultipleAsync" was implemented, this would be the target.
                results.Add(await queue.CountAsync());
            }
            return results;
        }

        public static async Task<List<bool>> RemoveMultipleAsync<TPriority, TItem>(this AsyncPriorityQueue<TPriority, TItem> queue, IEnumerable<TItem> items)
            where TPriority : IComparable<TPriority>
        {
            var results = new List<bool>();
            foreach (var item in items)
            {
                results.Add(await queue.RemoveAsync(item));
            }
            return results;
        }
    }
}
