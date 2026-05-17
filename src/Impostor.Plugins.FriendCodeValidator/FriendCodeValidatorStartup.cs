using Impostor.Api.Events;
using Impostor.Api.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Impostor.Plugins.FriendCodeValidator
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
