using Empostor.Api.Admin;
using Empostor.Api.Commands;
using Empostor.Api.Events;
using Empostor.Api.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Empostor.Plugins.PlayerStats;

public sealed class PlayerStatsPluginStartup : IPluginStartup
{
    public void ConfigureHost(IHostBuilder host) { }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<PlayerStatsStore>();
        services.AddSingleton<IEventListener, PlayerStatsListener>();
        services.AddSingleton<IAdminExtension, PlayerStatsAdminExtension>();
        services.AddSingleton<ICommand, StatCommand>();
    }
}
