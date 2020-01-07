using System;
using System.Threading;
using System.Threading.Tasks;
using KubeClient.Core.Extensions;
using KubeClient.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace KubeClient
{
    public class Startup
    {
        private IConfiguration Configuration { get; set; }
        private readonly ILogger _logger;
        private readonly IServiceProvider _serviceProvider;

        public Startup()
        {
            var builder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", false, true);

            Configuration = builder.Build();


            _serviceProvider = new ServiceCollection()
                .AddLogging()
                .AddTransient(a => Configuration)
                .AddKubClientServices()
                .BuildServiceProvider();

            var loggerFactory = _serviceProvider.GetService<ILoggerFactory>();

            _logger = CreateLoggger(loggerFactory);
        }

        private ILogger CreateLoggger(ILoggerFactory loggerFactory)
        {
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(Configuration)
                .CreateLogger();
            loggerFactory.AddSerilog();
            return loggerFactory.CreateLogger<Startup>();
        }

        public Task RunAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting kubernetes port forwarder.");

            var portForwarder = _serviceProvider.GetService<IPortForwardService>();
            return portForwarder.RunAsync(cancellationToken);
        }
    }
}