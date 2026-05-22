using Empostor.Api.Events;
using Empostor.Api.Plugins;
using Empostor.Plugin.Code.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Empostor.Plugin.Code;

public sealed class CodeStartup : IPluginStartup
{
    public void ConfigureHost(IHostBuilder host) { }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IGameCodeManager, GameCodeManager>();
        services.AddSingleton<IEventListener, GameEventListener>();
    }
}
