using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RedShirt.Example.Api.Common.RateLimiting.Configuration;
using RedShirt.Example.Api.Common.RateLimiting.Constants;
using RedShirt.Example.Api.Common.RateLimiting.Factories;

namespace RedShirt.Example.Api.Common.RateLimiting.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication ConsiderAddingRateLimitParts(this WebApplication app)
    {
        /*
         * Though our service provider has been built by this point,
         * still going through Configuration to parse options here
         * because the ServiceCollectionExtension uses configuration
         * to determine whether to bother loading in option configuration at all.
         */
        if (app.Configuration.GetSection(ConfigurationConstants.ConfigurationSectionName)
                .Get<GeneralRateLimiterOptions>() is
            {
                DisableRateLimiting: true
            })
        {
            return app;
        }

        var configurationModel = app.Services.GetRequiredService<IOptions<GeneralRateLimiterOptions>>();

        app
            // ReSharper disable once RedundantDelegateCreation
            .Use(new Func<HttpContext, Func<Task>, Task>(async (context, next) =>
            {
                var endpoint = context.GetEndpoint();
                var enable = endpoint?.Metadata.GetMetadata<EnableRateLimitingAttribute>();
                var disabled = endpoint?.Metadata.GetMetadata<DisableRateLimitingAttribute>();
                if (!configurationModel.Value.DisableRateLimiting && disabled is null)
                {
                    var factory = context.RequestServices
                        .GetRequiredService<ISlidingWindowRateLimiterFactoryFactory>();
                    context.Items[HttpContextConstants.FactoryItemKey] =
                        await factory.GetFactoryAsync(enable?.PolicyName ?? configurationModel.Value.DefaultPolicyName,
                            context.RequestAborted);
                }

                await next();
            }))
            /*
             * Note: It is important that UseRateLimiter be invoked AFTER the above Use call.
             * The rate-limiting policies depend on information set in the callback defined above in Use.
             */
            .UseRateLimiter();

        return app;
    }
}