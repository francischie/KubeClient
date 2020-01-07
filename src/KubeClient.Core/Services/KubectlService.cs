using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KubeClient.Core.Models;

namespace KubeClient.Core.Services
{
    public interface IKubectlService
    {
        Task<List<string>> GetPodsAsync(string clusterName, string namespaceName, CancellationToken cancellationToken);
        Task PortForwardAsync(PodMapping podMapping, CancellationToken token = default);
    }

    public class KubectlService : IKubectlService
    {
        private readonly ICacheService _cacheService;

        public KubectlService(ICacheService cacheService)
        {
            _cacheService = cacheService;
        }

        public Task<List<string>> GetPodsAsync(string clusterName, string namespaceName,
            CancellationToken cancellationToken = default)
        {
            var key = $"{clusterName}.{namespaceName}.pods";
            return _cacheService.GetOrCreateAsync(key, async entry =>
            {
                var command = CreateParameter("get pods ",  clusterName, namespaceName);
                var output =  await ExecuteCommandAsync(command);
                var names = output.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                var list = names.Skip(1).Select(a => System.Text.RegularExpressions.Regex.Split(a, @"\s{2,}")[0]).ToList();
                entry.AbsoluteExpiration = list.Count > 0 
                    ? DateTimeOffset.Now.AddMinutes(1) 
                    : DateTimeOffset.FromUnixTimeMilliseconds(1);
                return list; 
            });
          
        }

        private static string CreateParameter(string command, string clusterName, string namespaceName)
        {
            if (!string.IsNullOrEmpty(clusterName))
                command += $"--context={clusterName} ";

            if (!string.IsNullOrWhiteSpace(namespaceName))
                command += $"--namespace={namespaceName} ";
            
            return command;
        }

        private async Task<string> ExecuteCommandAsync(string command, bool redirectOutput = true,
            CancellationToken cancellationToken = default)
        {
            var result = await Task<string>.Factory.StartNew(() =>
            {
                var proc = CreateKubectlProcess( command, redirectOutput);
                proc.Start();
                var output = redirectOutput ? proc.StandardOutput.ReadToEnd() : string.Empty;
                proc.WaitForExit();
                return output;
            }, cancellationToken);
            return result;
        }
        
        public  async Task PortForwardAsync(PodMapping podMapping, CancellationToken cancellationToken = default)
        {
            var command = CreateParameter("port-forward ", podMapping.Cluster, podMapping.Namespace);
            var podName = await FindClosestNameAsync(podMapping, cancellationToken);
            
            if (string.IsNullOrWhiteSpace(podName))
                throw new Exception($"Can't find pod name: {podMapping.Name}"); 

            command += $"{podName} {podMapping.LocalPort}:{podMapping.RemotePort}";
            await ExecuteCommandAsync(command, false, cancellationToken);
        }
        
        private async Task<string> FindClosestNameAsync(PodMapping podMapping, CancellationToken cancellationToken = default)
        {
            var pods = await GetPodsAsync(podMapping.Cluster, podMapping.Namespace, cancellationToken);
            return pods.LastOrDefault(a => a == podMapping.Name || a.StartsWith(podMapping.Name));
        }

        private static Process CreateKubectlProcess(string command, bool redirectOutput = true)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "kubectl",
                Arguments = command,
                UseShellExecute = false,
                RedirectStandardOutput = redirectOutput,
                RedirectStandardError = redirectOutput,
                CreateNoWindow = true,
            };
            var proc = new Process { StartInfo = psi };
            return proc; 
        }
        
    }
}