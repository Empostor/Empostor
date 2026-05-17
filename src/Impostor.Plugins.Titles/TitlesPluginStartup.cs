using Impostor.Api.Events;
using Impostor.Api.Plugins;
using Impostor.Plugins.Titles.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Impostor.Plugins.Titles;

public sealed class TitlesPluginStartup : IPluginStartup
{
    private string _configPath = "[Title System]Config.json";

    public void SetConfigPath(string configPath) => _configPath = configPath;

    public void ConfigureHost(IHostBuilder host) { }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(_ => PluginConfigLoader.Load<TitlesConfig>(_configPath));
        services.AddSingleton<IEventListener, TitleEventListener>();
        services.AddSingleton<IEventListener, FriendCodeTitleListener>();
    }
}
