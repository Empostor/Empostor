using Microsoft.AspNetCore.Builder;

namespace Empostor.Api.Plugins;

public interface IPluginHttpStartup : IPluginStartup
{
    void ConfigureWebApplication(IApplicationBuilder builder);
}
