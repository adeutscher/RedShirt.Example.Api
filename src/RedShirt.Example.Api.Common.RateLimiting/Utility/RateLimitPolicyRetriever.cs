using Microsoft.Extensions.Configuration;
using RedShirt.Example.Api.Common.RateLimiting.Configuration;
using RedShirt.Example.Api.Common.RateLimiting.Constants;

namespace RedShirt.Example.Api.Common.RateLimiting.Utility;

internal static class RateLimitPolicyRetriever
{
    public static Dictionary<string, RateLimitingPolicyOptions> GetPolicies(IConfiguration configuration)
    {
        if (configuration
                .GetSection(
                    $"{ConfigurationConstants.ConfigurationSectionName}")
                .Get<GeneralRateLimiterOptions>() is {DisableRateLimiting: true})
        {
            return new Dictionary<string, RateLimitingPolicyOptions>();
        }

        var rawPoliciesDict = configuration
            .GetSection(
                $"{ConfigurationConstants.ConfigurationSectionName}:{ConfigurationConstants.PolicySubSectionName}")
            .Get<Dictionary<string, RateLimitingPolicyOptions>>();

        if (rawPoliciesDict is null || rawPoliciesDict.Count == 0)
        {
            throw new InvalidOperationException("No rate-limit policies have been configured.");
        }

        var returnDict = new Dictionary<string, RateLimitingPolicyOptions>();
        foreach (var kvp in rawPoliciesDict)
        {
            returnDict[string.IsNullOrWhiteSpace(kvp.Value.Name) ? kvp.Key : kvp.Value.Name] = kvp.Value;
        }

        return returnDict;
    }
}