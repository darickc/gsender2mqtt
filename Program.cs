using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using gsender2mqtt.Models;

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
                services.AddSingleton<Config>();
                Config config = new Config();
                hostContext.Configuration.Bind(config);
                services.AddSingleton(config);
                services.AddHostedService<SocketIOService>();
            })
            .Build();

        await host.RunAsync();
    }
}
