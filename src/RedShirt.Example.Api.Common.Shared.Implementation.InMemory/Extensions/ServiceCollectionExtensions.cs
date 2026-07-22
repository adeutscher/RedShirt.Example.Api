using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.Common.Shared.Core.Services;
using RedShirt.Example.Api.Common.Shared.Implementation.InMemory.Services;

namespace RedShirt.Example.Api.Common.Shared.Implementation.InMemory.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInMemorySharedImplementations(this IServiceCollection services)
    {
        return services
            .AddSingleton<IDataCacheService, InMemoryDataCacheService>()
            .AddSingleton<IAbstractedLockService, InMemoryLockService>();
    }
}