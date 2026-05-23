using Empostor.Api.Commands;
using Empostor.Api.Events;
using Empostor.Api.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Empostor.Plugins.MapVote;

public sealed class MapVoteStartup : IPluginStartup
{
    private string _configPath = "config";

    public void SetConfigPath(string configPath) => _configPath = configPath;

    public void ConfigureHost(IHostBuilder host) { }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(_ => PluginConfigLoader.Load<MapVoteConfig>(_configPath));
        services.AddSingleton<MapVoteService>();
        services.AddSingleton<MapVoteCommand>();
        services.AddSingleton<ICommand, MapVoteCommand>(sp => sp.GetRequiredService<MapVoteCommand>());
        services.AddSingleton<MapVoteEventListener>();
        services.AddSingleton<IEventListener, MapVoteEventListener>(sp => sp.GetRequiredService<MapVoteEventListener>());
    }
}
