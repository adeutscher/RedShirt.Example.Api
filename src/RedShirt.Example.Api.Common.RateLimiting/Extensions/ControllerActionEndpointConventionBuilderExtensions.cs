using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using RedShirt.Example.Api.Common.RateLimiting.Configuration;
using RedShirt.Example.Api.Common.RateLimiting.Constants;

namespace RedShirt.Example.Api.Common.RateLimiting.Extensions;

public static class ControllerActionEndpointConventionBuilderExtensions
{
    public static ControllerActionEndpointConventionBuilder ConsiderRequiringRateLimiting(
        this ControllerActionEndpointConventionBuilder endpointConventionBuilder, IConfiguration configuration)
    {
        var defaultPolicyName = configuration
            .GetSection($"{ConfigurationConstants.ConfigurationSectionName}")
            .Get<GeneralRateLimiterOptions>()
            ?.DefaultPolicyName;

        if (string.IsNullOrEmpty(defaultPolicyName))
        {
            return endpointConventionBuilder;
        }

        /*
         * Only endpoints that name no policy get the default.
         * RequireRateLimiting would add the default after the controller and action attributes, and ASP.NET Core reads
         *  the last EnableRateLimitingAttribute, so the default would replace every policy named on an endpoint.
         * Attribute metadata is already on the endpoint when conventions run, so it can be checked here.
         */
        endpointConventionBuilder.Add(endpointBuilder =>
        {
            if (endpointBuilder.Metadata.Any(m => m is EnableRateLimitingAttribute or DisableRateLimitingAttribute))
            {
                return;
            }

            endpointBuilder.Metadata.Add(new EnableRateLimitingAttribute(defaultPolicyName));
        });

        return endpointConventionBuilder;
    }
}