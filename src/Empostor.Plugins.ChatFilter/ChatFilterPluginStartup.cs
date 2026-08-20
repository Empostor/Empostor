using Empostor.Api.Admin;
using Empostor.Api.Events;
using Empostor.Api.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Empostor.Plugins.ChatFilter;

public sealed class ChatFilterPluginStartup : IPluginStartup
{
    public void ConfigureHost(IHostBuilder host) { }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ChatFilterStore>();
        services.AddSingleton<IEventListener, ChatFilterListener>();
        services.AddSingleton<IAdminExtension, ChatFilterAdminExtension>();
    }
}
