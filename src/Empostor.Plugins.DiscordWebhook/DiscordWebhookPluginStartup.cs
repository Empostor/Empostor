using Empostor.Api.Admin;
using Empostor.Api.Events;
using Empostor.Api.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Empostor.Plugins.DiscordWebhook;

public sealed class DiscordWebhookPluginStartup : IPluginStartup
{
    public void ConfigureHost(IHostBuilder host) { }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddSingleton<DiscordWebhookStore>();
        services.AddSingleton<IEventListener, DiscordWebhookListener>();
        services.AddSingleton<IAdminExtension, DiscordWebhookAdminExtension>();
    }
}
