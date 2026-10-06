using Empostor.Api.Admin;
using Empostor.Api.Events;
using Empostor.Api.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Empostor.Plugins.Maintenance;

public sealed class MaintenanceStartup : IPluginStartup
{
    public void ConfigureHost(IHostBuilder host) { }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<MaintenanceStore>();
        services.AddSingleton<MaintenanceListener>();
        services.AddSingleton<IEventListener>(sp => sp.GetRequiredService<MaintenanceListener>());
        services.AddSingleton<IAdminExtension>(sp => new MaintenanceAdminExtension(
            sp.GetRequiredService<MaintenanceStore>(),
            sp.GetRequiredService<MaintenanceListener>()));
    }
}
