using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace KubeClient.Core.Services
{
    public interface ICacheService
    {
        Task<T> GetOrCreateAsync<T>(string key, Func<ICacheEntry, Task<T>> factory, CancellationToken cancellationToken = default);
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

        public Task<T> GetOrCreateAsync<T>(string key, Func<ICacheEntry, Task<T>> factory, CancellationToken cancellationToken = default)
        {
            return _cache.GetOrCreateAsync(key, async entry =>
            {
                await _lock.WaitAsync(cancellationToken);
                var result = await factory.Invoke(entry);
                _lock.Release();
                return result;
            });
        }
    }
    
}