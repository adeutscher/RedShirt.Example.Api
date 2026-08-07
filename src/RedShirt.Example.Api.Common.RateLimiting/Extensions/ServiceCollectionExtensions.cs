using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.Common.RateLimiting.Configuration;
using RedShirt.Example.Api.Common.RateLimiting.Constants;
using RedShirt.Example.Api.Common.RateLimiting.Factories;
using RedShirt.Example.Api.Common.RateLimiting.Services;
using RedShirt.Example.Api.Common.RateLimiting.Utility;
using System.Threading.RateLimiting;

namespace RedShirt.Example.Api.Common.RateLimiting.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection ConsiderAddingRateLimitingPolicies(this IServiceCollection services,
        IConfiguration configuration)
    {
        if (configuration.GetSection(ConfigurationConstants.ConfigurationSectionName)
                .Get<GeneralRateLimiterOptions>() is
            {
                DisableRateLimiting: true
            })
        {
            return services;
        }

        services
            .AddHttpContextAccessor()
            .Configure<GeneralRateLimiterOptions>(
                configuration.GetSection(ConfigurationConstants.ConfigurationSectionName))
            .AddSingleton<ISlidingWindowRateLimiterFactoryFactory, SlidingWindowRateLimiterFactoryFactory>()
            .AddTransient<IInMemorySlidingWindowFactory, InMemorySlidingWindowFactory>()
            .AddTransient<IRedisSlidingWindowRateLimiterFactory, RedisSlidingWindowFactory>()
            // Utility Services
            .AddSingleton<IPartitionKeyResolverService, PartitionKeyResolverService>();

        return services
            .AddRateLimiter(rateLimitingOptions =>
            {
                rateLimitingOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                rateLimitingOptions.OnRejected = async (context, cancellationToken) =>
                {
                    if (context.Lease.TryGetMetadata(RateLimitMetadata.PermitLimit, out var limit))
                    {
                        context.HttpContext.Response.Headers[RateLimitHeaderNames.Limit] = limit.ToString();
                    }

                    context.HttpContext.Response.Headers[RateLimitHeaderNames.Remaining] =
                        context.Lease.TryGetMetadata(RateLimitMetadata.RemainingPermits, out var remaining)
                            ? remaining.ToString()
                            : "0";

                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    {
                        context.HttpContext.Response.Headers.RetryAfter =
                            ((int) retryAfter.TotalSeconds).ToString();
                    }

                    await context.HttpContext.Response.WriteAsync(
                        "Rate limit exceeded. Try again later.",
                        cancellationToken);
                };

                foreach (var policy in RateLimitPolicyRetriever.GetPolicies(configuration))
                {
                    rateLimitingOptions.AddPolicy(policy.Key, httpContext =>
                    {
                        var resolver = httpContext.RequestServices.GetRequiredService<IPartitionKeyResolverService>();

                        if (!httpContext.Items.TryGetValue(HttpContextConstants.FactoryItemKey, out var factoryObject)
                            || factoryObject is not ISlidingWindowRateLimiterFactory factory)
                        {
                            throw new InvalidOperationException(
                                "Unable to retrieve rate limiter component from previous middleware step.");
                        }

                        return factory.GetRateLimiter(resolver.ResolvePartitionKey(httpContext), policy.Value);
                    });
                }
            });
    }
}