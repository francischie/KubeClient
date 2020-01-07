namespace KubeClient.Core
{
    public class PodListService
    {
        private readonly ICacheService _cache;
        
        public PodListService(ICacheService cache)
        {
            _cache = cache;
        }
    }
}