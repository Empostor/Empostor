using Empostor.Api.Admin;
using Empostor.Api.Events;
using Empostor.Api.Plugins;
using Empostor.Plugins.Titles.Service;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Empostor.Plugins.Titles;

public sealed class TitlesPluginStartup : IPluginHttpStartup
{
    private string _configPath = "[Title System]Config.json";

    public void SetConfigPath(string configPath) => _configPath = configPath;

    public void ConfigureHost(IHostBuilder host) { }

    public void ConfigureServices(IServiceCollection services)
    {
        var config = PluginConfigLoader.Load<TitlesConfig>(_configPath);
        services.AddSingleton(config);
        services.AddSingleton<TitleStore>();
        services.AddSingleton<FriendCodeTitleListener>();
        services.AddSingleton<IEventListener>(sp => sp.GetRequiredService<FriendCodeTitleListener>());
        services.AddSingleton<IEventListener, TitleEventListener>();
        services.AddSingleton<IAdminExtension>(
            sp => new TitlesAdminExtension(config, _configPath, sp.GetRequiredService<FriendCodeTitleListener>()));
    }

    public void ConfigureWebApplication(IApplicationBuilder builder)
    {
        builder.UseMiddleware<TitleApiMiddleware>();
    }
}
