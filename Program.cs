using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using gsender2mqtt.Models;
using Microsoft.Extensions.Logging;
using Serilog;

namespace gsender2mqtt;
class Program
{
    static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((hostContext, config) =>
            {
                config.AddEnvironmentVariables();
            })
            .ConfigureServices((hostContext, services) =>
            {
                Config config = new Config();
                hostContext.Configuration.Bind(config);
                services.AddSingleton(config);
                services.AddHostedService<SocketIOService>();
            })
            .ConfigureLogging((hostingContext, logging) =>
                {
                    logging.ClearProviders();
                    Log.Logger = new LoggerConfiguration()
                        .ReadFrom.Configuration(hostingContext.Configuration)
                        .CreateLogger();
                    logging.AddSerilog();
                })
            .Build();

        await host.RunAsync();
    }
}
