using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KubeClient.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace KubeClient
{
    public class PortForwarder : IPortForwarder
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;
 

        public PortForwarder(IConfiguration configuration, ILoggerFactory loggerFactory)
        {
            _configuration = configuration;
            _logger = loggerFactory.CreateLogger<PortForwarder>();
        }
     
        public Task RunAsync(CancellationToken cancellationToken)
        {
            var podMappings = new List<PodMapping>();
            _configuration.GetSection("pods").Bind(podMappings);
            
            return podMappings.ForEachAsync(a => ForwardAsync(a, cancellationToken));
        }

        private Task ForwardAsync(PodMapping pod, CancellationToken token)
        {
            return Task.Run(() =>
            {
                var cancelled = false; 

                while (true)
                {

                   
                    _logger.LogInformation($"Forwarding localhost:{pod.LocalPort} --> {pod.Cluster}.{pod.Namespace}.{pod.Name}:{pod.RemotePort}");
                    
                    var process = KubectlHelper.PortForward(pod);
                    
                    if (process == null)
                    {
                        _logger.LogInformation("Cannot find Pod named: {0}", pod.Name );
                        return;
                    }


                    token.Register(() =>
                    {
                        cancelled = true;
                        if (!process.HasExited)
                        {
                            process.Kill(true);
                        }
                    });

                    process.Start();
                    process.WaitForExit();

                    if (cancelled)
                    {
                        _logger.LogInformation("Port forwarding on {podName} was cancelled!", pod.Name);
                        break;
                    } 

                    _logger.LogInformation("Port forwarding on {podName} {localport} --> {remotePort} was terminated. Will try to reload", pod.Name, pod.LocalPort, pod.RemotePort);
                    Thread.Sleep(500);
                }
                
            }, token);
          
           
        }

      

    


    }
}