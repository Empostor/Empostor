using Empostor.Api.Events;
using Empostor.Api.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Empostor.Plugins.FriendCodeValidator
{
    public sealed class FriendCodeValidatorStartup : IPluginStartup
    {
        public void ConfigureHost(IHostBuilder host) { }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IEventListener, FriendCodeValidationListener>();
        }
    }
}
