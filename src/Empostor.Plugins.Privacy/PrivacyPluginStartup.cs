using Empostor.Api.Admin;
using Empostor.Api.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Empostor.Plugins.Privacy;

public sealed class PrivacyPluginStartup : IPluginStartup, IPluginHttpStartup
{
    public void ConfigureHost(IHostBuilder host) { }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<PrivacyStore>();
        services.AddSingleton<IAdminExtension, PrivacyAdminExtension>();
    }

    public void ConfigureWebApplication(IApplicationBuilder app)
    {
        app.UseMiddleware<PrivacyMiddleware>();
    }
}
