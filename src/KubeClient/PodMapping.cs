namespace KubeClient
{
    public class PodMapping
    {
        public int LocalPort { get; set; }
        public int RemotePort { get; set; }
        public string Name { get; set; }
        
        public string Namespace { get; set; }
        
        public string Cluster { get; set; }
    }
}