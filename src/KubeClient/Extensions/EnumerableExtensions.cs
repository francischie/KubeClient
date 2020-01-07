using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KubeClient.Extensions
{
    public static class EnumerableExtensions
    {
        public static async Task ForEachAsync<T>(this List<T> list, Func<T, Task> callback)
        {
            var tasks = new List<Task>();
            list.ForEach(a => { tasks.Add(Task.Run(() => callback(a))); });
            await Task.WhenAll(tasks);
        }
    }
}