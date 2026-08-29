using Empostor.Api.Admin;
using Empostor.Api.Events;
using Empostor.Api.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Empostor.Plugins.PlayerLog;

public sealed class PlayerLogPluginStartup : IPluginStartup
{
    public void ConfigureHost(IHostBuilder host) { }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<PlayerLogStore>();
        services.AddSingleton<IEventListener, PlayerLogListener>();
        services.AddSingleton<IAdminExtension, PlayerLogAdminExtension>();
    }
}
