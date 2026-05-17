using Impostor.Api.Events;
using Impostor.Api.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Impostor.Plugins.FixedCode;

public sealed class FixedCodeStartup : IPluginStartup
{
    private string _configPath = "[Fixed Room Code]Config.json";

    public void SetConfigPath(string configPath) => _configPath = configPath;

    public void ConfigureHost(IHostBuilder host) { }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(_ =>
            PluginConfigLoader.Load<FixedCodeConfig>(_configPath));

        services.AddSingleton<FixedCodeListener>();
        services.AddSingleton<IEventListener, FixedCodeListener>(
            sp => sp.GetRequiredService<FixedCodeListener>());
    }
}
