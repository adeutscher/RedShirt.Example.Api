using Microsoft.AspNetCore.Builder;
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

        if (!string.IsNullOrEmpty(defaultPolicyName))
        {
            endpointConventionBuilder.RequireRateLimiting(defaultPolicyName);
        }

        return endpointConventionBuilder;
    }
}