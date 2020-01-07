using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KubeClient.Core.UnitTests
{
    public class CacheServiceTests
    {
        private readonly ICacheService _cacheService;
        
        public CacheServiceTests()
        {
            var serviceProvider = new ServiceCollection()
                .AddMemoryCache()
                .AddSingleton<ICacheService, CacheService>()
                .BuildServiceProvider();
            
            _cacheService = ActivatorUtilities.CreateInstance<CacheService>(serviceProvider);
        }
        
        
        [Fact]
        public async Task GetOrCreateAsync_EnsureSingleCall_Test()
        {
            var counter = 0;
            const int numberOfThreads = 50; 
            var tasks = Enumerable.Range(0, numberOfThreads)
                .Select(i =>
                {
                    return _cacheService.GetOrCreateAsync("Test", async entry =>
                    {
                        await Task.Delay(100);
                        Interlocked.Increment(ref counter);
                        return "something";
                    });
                });
            
            await Task.WhenAll(tasks);
            
            Assert.Equal(1, counter);
            
        }
    }
}