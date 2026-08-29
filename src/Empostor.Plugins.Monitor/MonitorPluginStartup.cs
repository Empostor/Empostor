using Empostor.Api.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Empostor.Plugins.Monitor;

public sealed class MonitorPluginStartup : IPluginStartup, IPluginHttpStartup
{
    public void ConfigureHost(IHostBuilder host) { }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers()
            .AddApplicationPart(typeof(MonitorPluginStartup).Assembly);
    }

    public void ConfigureWebApplication(IApplicationBuilder app)
    {
    }
}
