using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KubeClient.Core.Extensions;
using KubeClient.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace KubeClient.Core.Services
{
    public interface IPortForwardService
    {
        Task RunAsync(CancellationToken cancellationToken);
    }

    public class PortForwardService : IPortForwardService
    {
        private readonly ILogger<PortForwardService> _logger;
        private readonly IKubectlService _kubectlService;
        private readonly IConfiguration _configuration;
        


        public PortForwardService(ILogger<PortForwardService> logger, IKubectlService kubectlService, IConfiguration configuration)
        {
            _logger = logger;
            _kubectlService = kubectlService;
            _configuration = configuration;
        }

        public Task RunAsync(CancellationToken cancellationToken)
        {
            var podMappings = new List<PodMapping>();
            _configuration.GetSection("pods").Bind(podMappings);
            
            return podMappings.ForEachAsync(a => StartAsync(a, cancellationToken));
        }
        
        private Task StartAsync(PodMapping pod, CancellationToken token)
        {
            return Task.Run(async () =>
            {
                while (token.IsCancellationRequested == false)
                {
                    var source = $"localhost:{pod.LocalPort}";
                    var target = $"{pod.Cluster}.{pod.Namespace}.{pod.Name}:{pod.RemotePort}";
                    
                    _logger.LogInformation("Forwarding {0} to {1}", source, target);
                    
                    await _kubectlService.PortForwardAsync(pod, token);

                    _logger.LogInformation("Port forwarding on {0} --> {1} was terminated. Will try to reload", source,target);
                    await Task.Delay(500, token);
                }
                
            }, token);
          
           
        }
        
     
    }
    
    
}