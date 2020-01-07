using System.Threading;
using System.Threading.Tasks;

namespace KubeClient
{
    public interface IPortForwarder
    {
        Task RunAsync(CancellationToken cancellationToken);
        
    }
}