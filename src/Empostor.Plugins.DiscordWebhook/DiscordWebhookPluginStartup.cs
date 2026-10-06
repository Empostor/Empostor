using Empostor.Api.Admin;
using Empostor.Api.Commands;
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
        services.AddSingleton<LiveGameTracker>();
        services.AddSingleton<LiveRoomToggle>();
        services.AddSingleton<DiscordWebhookStore>();
        services.AddSingleton<DiscordWebhookListener>();
        services.AddSingleton<IEventListener>(sp => sp.GetRequiredService<DiscordWebhookListener>());
        services.AddSingleton<DiscordWebhookCommand>();
        services.AddSingleton<ICommand, DiscordWebhookCommand>(
            sp => sp.GetRequiredService<DiscordWebhookCommand>());
        services.AddSingleton<IAdminExtension, DiscordWebhookAdminExtension>();
        services.AddHostedService<WebhookOrphanCleanup>();
    }
}
