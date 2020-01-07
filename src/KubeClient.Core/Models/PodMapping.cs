namespace KubeClient.Core.Models
{
    public class PodMapping
    {
        public int LocalPort { get; set; }
        public int RemotePort { get; set; }
        public string Name { get; set; }
        
        public string Namespace { get; set; }
        
        public string ClusterName { get; set; }
    }
}