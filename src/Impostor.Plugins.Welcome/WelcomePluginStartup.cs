using Impostor.Api.Events;
using Impostor.Api.Plugins;
using Impostor.Plugins.Welcome.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Impostor.Plugins.Welcome;

public sealed class WelcomePluginStartup : IPluginStartup
{
    public void ConfigureHost(IHostBuilder host) { }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IEventListener, WelcomeEventListener>();
    }
}
