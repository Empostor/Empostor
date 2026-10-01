using System;
using System.Security.Cryptography;
using System.Text;
using Empostor.Api.Admin;
using Empostor.Api.Config;
using Empostor.Api.Events;
using Empostor.Api.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Empostor.Plugins.PlayerLog;

public sealed class PlayerLogPluginStartup : IPluginStartup, IPluginHttpStartup
{
    public void ConfigureHost(IHostBuilder host) { }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<PlayerLogStore>();
        services.AddSingleton<IEventListener, PlayerLogListener>();
        services.AddSingleton<IAdminExtension, PlayerLogAdminExtension>();
    }

    public void ConfigureWebApplication(IApplicationBuilder app)
    {
        app.Map("/player-log/export", branch => branch.Run(async context =>
        {
            if (!IsAdmin(context))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            var store = context.RequestServices.GetRequiredService<PlayerLogStore>();
            var raw = context.Request.Query["clientId"].ToString();
            var bytes = int.TryParse(raw, out var clientId) ? store.ExportJson(clientId) : store.ExportJson();

            context.Response.ContentType = "application/json";
            context.Response.Headers["Content-Disposition"] = "attachment; filename=player_logs.json";
            await context.Response.Body.WriteAsync(bytes);
        }));
    }

    private static bool IsAdmin(HttpContext context)
    {
        var config = context.RequestServices.GetRequiredService<IOptions<AdminConfig>>().Value;
        if (string.IsNullOrEmpty(config.Password))
        {
            return false;
        }

        return context.Request.Cookies.TryGetValue("empostor_admin", out var value)
               && string.Equals(value, Hash(config.Password), StringComparison.Ordinal);
    }

    private static string Hash(string input)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
}
