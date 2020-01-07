using System.Collections.Generic;
using System.Linq;
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


        public PortForwardService(ILogger<PortForwardService> logger, IKubectlService kubectlService,
            IConfiguration configuration)
        {
            _logger = logger;
            _kubectlService = kubectlService;
            _configuration = configuration;
        }

        public Task RunAsync(CancellationToken cancellationToken)
        {
            var mappings = new List<PodMapping>();
            _configuration.GetSection("pods").Bind(mappings);

            ForwardStaticCluster(mappings, cancellationToken);

            return ForwardDynamicClusterAsync(mappings, cancellationToken);
        }

        private Task MapPodAsync(PodMapping pod, CancellationToken token)
        {
            return Task.Run(async () =>
            {
                while (token.IsCancellationRequested == false)
                {
                    var clusterName = string.IsNullOrWhiteSpace(pod.ClusterName)
                        ? await _kubectlService.GetCurrentContextAsync()
                        : pod.ClusterName;
                    
                    var source = $"localhost:{pod.LocalPort}";
                    var target = $"{clusterName}.{pod.Namespace}.{pod.Name}:{pod.RemotePort}";

                    _logger.LogInformation("Forwarding {0} to {1}", source, target);

                    var process = await _kubectlService.PortForwardAsync(pod, token);
                    token.Register(() =>
                    {
                        if (!process.HasExited)
                            process.Kill(true);
                    });
                    process.Start();
                    process.WaitForExit();
                    

                    _logger.LogInformation("Port forwarding on {0} --> {1} was terminated. Will try to reload", source,
                        target);
                    await Task.Delay(500);
                }
            }, token);
        }

        private async Task ForwardDynamicClusterAsync(List<PodMapping> mappings, CancellationToken cancellationToken)
        {
            while (cancellationToken.IsCancellationRequested == false)
            {
                var monitorCancellationToken = await StartMonitorContextChangeAsync(cancellationToken);
                await mappings.Where(a => string.IsNullOrWhiteSpace(a.ClusterName))
                    .ToList()
                    .ForEachAsync(a => MapPodAsync(a, monitorCancellationToken));
                await Task.Delay(1000, cancellationToken);
            }
        }

        private async Task<CancellationToken> StartMonitorContextChangeAsync(CancellationToken cancellationToken)
        {
            var tokenSource = new CancellationTokenSource();
            var monitorCancellationToken = tokenSource.Token;
            var currentContext = await _kubectlService.GetCurrentContextAsync();
            
#pragma warning disable 4014
            Task.Run(async () =>
            {
                while (cancellationToken.IsCancellationRequested == false)
                {
                    var newContext = await _kubectlService.GetCurrentContextAsync();
                    if (currentContext == newContext)
                    {
                        await Task.Delay(1000, cancellationToken);
                        continue;
                    }
                    tokenSource.Cancel();
                    break;
                }
            }, cancellationToken);
#pragma warning restore 4014
          
            return monitorCancellationToken;


        }
        

        private void ForwardStaticCluster(List<PodMapping> mappings, CancellationToken cancellationToken)
        {
#pragma warning disable 4014 
            mappings.Where(a => string.IsNullOrWhiteSpace(a.ClusterName) == false)
                .ToList()
                .ForEachAsync(a => MapPodAsync(a, cancellationToken));
#pragma warning restore 4014
        }
    }
}