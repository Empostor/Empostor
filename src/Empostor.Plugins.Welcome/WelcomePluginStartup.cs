using Empostor.Api.Events;
using Empostor.Api.Plugins;
using Empostor.Plugins.Welcome.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Empostor.Plugins.Welcome;

public sealed class WelcomePluginStartup : IPluginStartup
{
    public void ConfigureHost(IHostBuilder host) { }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IEventListener, WelcomeEventListener>();
    }
}
