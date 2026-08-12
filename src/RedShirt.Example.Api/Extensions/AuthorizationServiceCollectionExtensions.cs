using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using RedShirt.Example.Api.Authorization;
using RedShirt.Example.Api.Authorization.Constants;
using RedShirt.Example.Api.Authorization.Requirements;
using RedShirt.Example.Api.Authorization.ResourceScoping.Customer;

namespace RedShirt.Example.Api.Extensions;

internal static class AuthorizationServiceCollectionExtensions
{
    private static AuthorizationPolicyBuilder ConfigureApiPolicy(AuthorizationPolicyBuilder policy)
    {
        return policy
            .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser();
    }

    internal static IServiceCollection AddApiAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddSingleton<IClaimsTransformation, BespokeRolePermissionClaimsTransformation>();
        services.AddSingleton<IAuthorizationHandler, HttpGetAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler, CustomerScopedResourceAuthorizationHandler>();
        services.AddSingleton<ICustomerScopedResourceEnforcer, CustomerScopedResourceEnforcer>();

        services.AddAuthorization(authorization =>
        {
            authorization.AddPolicy(BespokeAuthorizationPolicies.Write, policy => ConfigureApiPolicy(policy)
                .RequireClaim(BespokeAuthorizationPermissions.ClaimType, BespokeAuthorizationPermissions.Write));

            authorization.AddPolicy(BespokeAuthorizationPolicies.ReadApproved, policy => ConfigureApiPolicy(policy)
                .RequireClaim(BespokeAuthorizationPermissions.ClaimType, BespokeAuthorizationPermissions.Read)
                .AddRequirements(new HttpGetRequirement()));

            authorization.AddPolicy(BespokeAuthorizationPolicies.CustomerScoped, policy => ConfigureApiPolicy(policy)
                .AddRequirements(new CustomerScopedResourceRequirement()));

            authorization.FallbackPolicy = authorization.GetPolicy(BespokeAuthorizationPolicies.Write);
        });

        return services;
    }
}