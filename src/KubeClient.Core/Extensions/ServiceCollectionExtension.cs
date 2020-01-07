using Microsoft.Extensions.DependencyInjection;
using NetCore.AutoRegisterDi;

namespace KubeClient.Core.Extensions
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddKubClientServices(this IServiceCollection services)
        {
            services.RegisterAssemblyPublicNonGenericClasses()
                .Where(a => a.Name.EndsWith("Service"))
                .AsPublicImplementedInterfaces();

            services.AddMemoryCache();
            
            return services;
        }
    }
}