using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace KubeClient
{
    public class PortForwarder : IPortForwarder
    {
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        private readonly ILogger _logger;
        private object _lockObject = new Object();
        private string _currentContext;
        private CancellationTokenSource _cancellationToken;
        private List<string> KubePods
        {
            get
            {
                lock (_lockObject)
                {
                    return _cache.GetOrCreate("kube_pods", entry =>
                    {
                        _logger.LogInformation("Refreshing list of kubernetes pods.");
                        var pods = KubectlHelper.GetPods();
                        entry.SetAbsoluteExpiration(TimeSpan.FromSeconds(30));
                        return pods;
                    });
                }
            }
        }


        public PortForwarder(IConfiguration configuration, ILoggerFactory loggerFactory, IMemoryCache cache)
        {
            _configuration = configuration;
            _cache = cache;
            _logger = loggerFactory.CreateLogger<PortForwarder>();
        }

        private bool RefreshContext()
        {
            var newContext = KubectlHelper.GetCurrentContext();
            if (newContext != null && newContext != _currentContext)
            {
                _logger.LogInformation("Context change {currentContext} --> {newContext}", _currentContext, newContext);
                _currentContext = newContext;
                _cache.Remove("kube_pods");
                return true;
            }
            return false;
        }


        public void Run()
        {
            _currentContext = KubectlHelper.GetCurrentContext();

            _cancellationToken = new CancellationTokenSource();

            var podMappings = new List<PodMapping>();
            _configuration.GetSection("pods").Bind(podMappings);

            _logger.LogInformation("Current Context: {context}", _currentContext);
            _logger.LogInformation("Running all port-forwarder and keep it alive.");

            var task = Task.Run(() =>
            {
                podMappings.ForEach(t => ForwardAsync(t, _cancellationToken.Token));
                while (true)
                {
                    if (RefreshContext())
                    {
                        _cancellationToken.Cancel();
                        _cancellationToken = new CancellationTokenSource();
                        podMappings.ForEach(t => ForwardAsync(t, _cancellationToken.Token));
                    }
                    Thread.Sleep(1000);
                }
                // ReSharper disable once FunctionNeverReturns
            });

            task.Wait();
        }

        private Task ForwardAsync(PodMapping pod, CancellationToken token)
        {
            return Task.Run(() =>
            {
                var cancelled = false; 

                while (true)
                {
                    var podName = FindClosestName(pod.Name);

                    if (string.IsNullOrEmpty(podName)) return;

                    _logger.LogInformation($"Forwarding localhost:{pod.LocalPort} --> {pod.Cluster}.{pod.Namespace}.{pod.Name}:{pod.RemotePort}");
                    
                    var process = KubectlHelper.PortForward(pod, podName);

                    token.Register(() =>
                    {
                        cancelled = true;
                        if (process != null && !process.HasExited)
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

      

        private string FindClosestName(string podToFind)
        {
            var match = KubePods.LastOrDefault(a => a == podToFind || a.StartsWith(podToFind));

            if (match != null) return match;

            _logger.LogInformation("Cannot find pod {podToFind}", podToFind);
            return null;
        }



    }
}