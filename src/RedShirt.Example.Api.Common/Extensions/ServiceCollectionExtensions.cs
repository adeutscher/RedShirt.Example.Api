using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.Common.Services.Utility;

namespace RedShirt.Example.Api.Common.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCommonServices(this IServiceCollection services)
    {
        return services
            .AddSingleton<ISleepService, SleepService>();
    }
}