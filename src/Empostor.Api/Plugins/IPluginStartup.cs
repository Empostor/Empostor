using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Empostor.Api.Plugins
{
    public interface IPluginStartup
    {
        void ConfigureHost(IHostBuilder host);

        void ConfigureServices(IServiceCollection services);

        void SetConfigPath(string configPath) { }
    }
}
