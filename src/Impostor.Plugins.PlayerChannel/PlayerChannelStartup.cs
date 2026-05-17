using Impostor.Api.Commands;
using Impostor.Api.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Impostor.Plugins.PlayerChannel;

public sealed class PlayerChannelStartup : IPluginStartup
{
    private string _configPath = "[Player Channel]Config.json";

    public void SetConfigPath(string configPath) => _configPath = configPath;

    public void ConfigureHost(IHostBuilder host) { }

    public void ConfigureServices(IServiceCollection services)
    {
        var config = PluginConfigLoader.Load<PlayerChannelConfig>(_configPath);
        services.AddSingleton(config);
        services.AddSingleton<PlayerChannelCommand>();
        services.AddSingleton<ICommand, PlayerChannelCommand>(
            sp => sp.GetRequiredService<PlayerChannelCommand>());
    }
}
