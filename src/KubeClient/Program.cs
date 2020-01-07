using System.Threading;
using System.Threading.Tasks;

namespace KubeClient
{
    class Program
    {
        static async Task Main()
        { 
            var cancellationToken = new CancellationTokenSource();
           var startup = new Startup();
           await startup.RunAsync(cancellationToken.Token);
        }

    }
}
