using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace KubeClient
{
    public static class KubectlHelper
    {
        public static string GetCurrentContext()
        {
            var output = ExecuteKubectlCommand("config current-context");
            return output.TrimEnd(); 
        }

        public static List<string> GetPods()
        {
            var output = ExecuteKubectlCommand("get pods");

            var names = output.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            return names.Skip(1).Select(a => System.Text.RegularExpressions.Regex.Split(a, @"\s{2,}")[0]).ToList();
        }

        public static Process PortForward(PodMapping podMapping, string podName)
        {
            var command = "port-forward ";
            if (!string.IsNullOrEmpty(podMapping.Cluster))
                command += $"--context={podMapping.Cluster} ";

            if (!string.IsNullOrWhiteSpace(podMapping.Namespace))
                command += $"--namespace={podMapping.Namespace} ";

            command += $"{podName} {podMapping.LocalPort}:{podMapping.RemotePort}";
            return CreateKubectlProcess(command, false);
        }

        private static string ExecuteKubectlCommand(string command, bool redirectOutput = true)
        {
            var proc = CreateKubectlProcess( command, redirectOutput);
            proc.Start();
            var output = redirectOutput ? proc.StandardOutput.ReadToEnd() : string.Empty;
            proc.WaitForExit();
            return output;
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