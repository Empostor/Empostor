using Impostor.Api.Commands;
using Impostor.Api.Plugins;
using Impostor.Api.Service.Admin.Verify;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Impostor.Plugins.QqVerify;

public sealed class QqVerifyStartup : IPluginStartup
{
    private string _configPath = "[QQ Verify]Config.json";

    public void SetConfigPath(string configPath) => _configPath = configPath;

    public void ConfigureHost(IHostBuilder host) { }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(_ => PluginConfigLoader.Load<QqVerifyConfig>(_configPath));
        services.AddSingleton<IVerifyStore, QqVerifyStore>();
        services.AddSingleton<QqVerifyCommand>();
        services.AddSingleton<ICommand, QqVerifyCommand>(
            sp => sp.GetRequiredService<QqVerifyCommand>());
    }
}
