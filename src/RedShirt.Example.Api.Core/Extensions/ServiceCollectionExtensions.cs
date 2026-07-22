using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.Core.Services;
using RedShirt.Example.Api.Core.Services.Topics.ExampleItem;

namespace RedShirt.Example.Api.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureApiCore(this IServiceCollection services,
        IConfiguration configuration)
    {
        return services
            .AddSingleton<ISubmissionIdempotencyService, SubmissionIdempotencyService>()
            .Configure<SubmissionIdempotencyService.ConfigurationModel>(configuration.GetSection("Core:Idempotency"))
            .AddSingleton<IExampleItemService, ExampleItemService>();
    }
}