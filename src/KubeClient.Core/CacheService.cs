using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace KubeClient.Core
{
    public interface ICacheService
    {
        Task<T> GetOrCreateAsync<T>(string key, Func<ICacheEntry, Task<T>> factory);
    }

    public class CacheService : ICacheService
    {
        private readonly IMemoryCache _cache;
        private readonly  SemaphoreSlim _lock; 
        
        public CacheService(IMemoryCache cache)
        {
            _cache = cache;
            _lock = new SemaphoreSlim(1,1);
        }

        public async Task<T> GetOrCreateAsync<T>(string key, Func<ICacheEntry, Task<T>> factory)
        {
            await _lock.WaitAsync();
            var result = await  _cache.GetOrCreateAsync(key, factory);
            _lock.Release();
            return result;
        }
    }
    
}