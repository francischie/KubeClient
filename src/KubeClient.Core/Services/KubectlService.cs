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
        Task<Process> PortForwardAsync(PodMapping podMapping, CancellationToken token = default);
        Task<string> GetCurrentContextAsync();
    }

    public class KubectlService : IKubectlService
    {
        private readonly ICacheService _cacheService;

        public KubectlService(ICacheService cacheService)
        {
            _cacheService = cacheService;
        }

        private Task<List<string>> GetPodsAsync(string clusterName, string namespaceName)
        {
            var key = $"{clusterName}.{namespaceName}.pods";
            return _cacheService.GetOrCreateAsync(key, async entry =>
            {
                var command = CreateCommand("get pods ", clusterName, namespaceName);
                var output = await ExecuteCommandAsync(command);
                var names = output.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                var list = names.Skip(1).Select(a => System.Text.RegularExpressions.Regex.Split(a, @"\s{2,}")[0])
                    .ToList();
                entry.AbsoluteExpiration = list.Count > 0
                    ? DateTimeOffset.Now.AddMinutes(1)
                    : DateTimeOffset.FromUnixTimeMilliseconds(1);
                return list;
            });
        }

        public Task<string> GetCurrentContextAsync()
        {
            return ExecuteCommandAsync("config current-context");
        }

        private string CreateCommand(string command, string clusterName, string namespaceName)
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
                var process = CreateKubectlProcess(command, redirectOutput);
                process.Start();
                var output = redirectOutput ? process.StandardOutput.ReadToEnd() : string.Empty;
                process.WaitForExit();
                return output;
            }, cancellationToken);
            return result.Trim();
        }

        public async Task<Process> PortForwardAsync(PodMapping podMapping, CancellationToken cancellationToken = default)
        {
            var command = CreateCommand("port-forward ", podMapping.ClusterName, podMapping.Namespace);
            var podName = await FindClosestNameAsync(podMapping);

            if (string.IsNullOrWhiteSpace(podName))
                throw new Exception($"Can't find pod name: {podMapping.Name}");

            command += $"{podName} {podMapping.LocalPort}:{podMapping.RemotePort}";
            return CreateKubectlProcess(command, false);
        }

        private async Task<string> FindClosestNameAsync(PodMapping podMapping)
        {
            var pods = await GetPodsAsync(podMapping.ClusterName, podMapping.Namespace);
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
            var proc = new Process {StartInfo = psi};
            return proc;
        }
    }
}