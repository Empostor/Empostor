using Empostor.Api.Admin;
using Empostor.Api.Commands;
using Empostor.Api.Events;
using Empostor.Api.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Empostor.Plugin.Narrator;

public sealed class NarratorStartup : IPluginStartup
{
    private string _configPath = "[Narrator]Config.json";

    public void SetConfigPath(string configPath) => _configPath = configPath;

    public void ConfigureHost(IHostBuilder host) { }

    public void ConfigureServices(IServiceCollection services)
    {
        var config = PluginConfigLoader.Load<NarratorConfig>(_configPath);
        services.AddSingleton(config);
        services.AddSingleton<NarratorService>();
        services.AddSingleton<NarratorCommand>();
        services.AddSingleton<ICommand, NarratorCommand>(
            sp => sp.GetRequiredService<NarratorCommand>());
        services.AddSingleton<NarratorEventListener>();
        services.AddSingleton<IEventListener, NarratorEventListener>(
            sp => sp.GetRequiredService<NarratorEventListener>());
        services.AddSingleton<IAdminExtension, NarratorAdminExtension>(
            _ => new NarratorAdminExtension(config, _configPath));
    }
}
