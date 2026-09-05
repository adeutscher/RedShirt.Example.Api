using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.ClientEvents.Domains.Example.Services;

namespace RedShirt.Example.Api.ClientEvents.Domains.Example.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddExampleClientEventsDomain(this IServiceCollection services)
    {
        return services
            .AddSingleton<IExampleMessageSendService, ExampleMessageSendService>()
            .AddSingleton<IExampleMessageReceiveService, ExampleMessageReceiveService>();
    }
}
