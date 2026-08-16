using Empostor.Server.Events.Player;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.ObjectPool;

namespace Empostor.Server
{
    public static class ServiceProviderExtensions
    {
        public static void AddEventPools(this IServiceCollection services)
        {
            // Larger object-pool retention so high-frequency packet allocation
            // (MessageReader, event objects) is served from the pool instead of
            // hammering the GC under load (e.g. 40+ players).
            services.TryAddSingleton<ObjectPoolProvider>(
                new DefaultObjectPoolProvider { MaximumRetained = 512 });

            services.AddSingleton(serviceProvider =>
            {
                var provider = serviceProvider.GetRequiredService<ObjectPoolProvider>();
                var policy = ActivatorUtilities.CreateInstance<PlayerMovementEvent.PlayerMovementEventObjectPolicy>(serviceProvider);
                return provider.Create(policy);
            });
        }
    }
}
