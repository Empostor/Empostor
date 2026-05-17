using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.ObjectPool;

namespace Next.Hazel.Extensions;

#nullable enable
public static class ServiceProviderExtensions
{
    public static IServiceCollection AddPolicy<TPolicy, TP>(this IServiceCollection services)
        where TPolicy : class, IPooledObjectPolicy<TP>
        where TP : class
    {
        return services.AddPolicy<TPolicy, TP, DefaultObjectPoolProvider>(new DefaultObjectPoolProvider());
    }

    public static IServiceCollection AddPolicy<TPolicy, TP, TPoolProvider>(this IServiceCollection services,
        TPoolProvider? Instance = null)
        where TPolicy : class, IPooledObjectPolicy<TP>
        where TPoolProvider : ObjectPoolProvider
        where TP : class
    {
        if (Instance != null)
            services.TryAddSingleton<ObjectPoolProvider>(Instance);
        else
            services.TryAddSingleton<ObjectPoolProvider, TPoolProvider>();

        return services.AddSingleton(serviceProvider =>
        {
            var provider = serviceProvider.GetRequiredService<ObjectPoolProvider>();
            var policy = ActivatorUtilities.CreateInstance<TPolicy>(serviceProvider);
            return provider.Create(policy);
        });
    }

    public static IServiceCollection AddHazel(this IServiceCollection services)
    {
        return services.AddPolicy<MessageReaderPolicy, MessageReader>();
    }
}