using Empostor.Api.Commands;
using Empostor.Api.Events;
using Empostor.Api.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Empostor.Plugins.Message;

public sealed class MessageStartup : IPluginStartup
{
    private string _configPath = "config";

    public void SetConfigPath(string configPath) => _configPath = configPath;

    public void ConfigureHost(IHostBuilder host) { }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(_ => PluginConfigLoader.Load<MessageConfig>(_configPath));
        services.AddSingleton<MessageStore>();
        services.AddSingleton<MessageCommand>();
        services.AddSingleton<ICommand, MessageCommand>(sp => sp.GetRequiredService<MessageCommand>());
        services.AddSingleton<MessageEventListener>();
        services.AddSingleton<IEventListener, MessageEventListener>(sp => sp.GetRequiredService<MessageEventListener>());
    }
}
