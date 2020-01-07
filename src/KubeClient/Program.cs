using System.Threading.Tasks;

namespace KubeClient
{
    class Program
    {
        static async Task Main()
        {
           var startup = new Startup();
           await startup.RunAsync();
        }

    }
}
