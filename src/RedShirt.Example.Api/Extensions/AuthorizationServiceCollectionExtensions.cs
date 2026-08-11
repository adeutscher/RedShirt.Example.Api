using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using RedShirt.Example.Api.Authorization;
using RedShirt.Example.Api.Constants;

namespace RedShirt.Example.Api.Extensions;

internal static class AuthorizationServiceCollectionExtensions
{
    internal static IServiceCollection AddApiAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddSingleton<IClaimsTransformation, RolePermissionClaimsTransformation>();
        services.AddSingleton<IAuthorizationHandler, HttpGetAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler, CustomerScopedResourceAuthorizationHandler>();
        services.AddSingleton<ICustomerScopedResourceAuthorizer, CustomerScopedResourceAuthorizer>();

        services.AddAuthorization(authorization =>
        {
            authorization.AddPolicy(AuthorizationPolicies.Write, policy => ConfigureApiPolicy(policy)
                .RequireClaim(AuthorizationPermissions.ClaimType, AuthorizationPermissions.Write));

            authorization.AddPolicy(AuthorizationPolicies.ReadApproved, policy => ConfigureApiPolicy(policy)
                .RequireClaim(AuthorizationPermissions.ClaimType, AuthorizationPermissions.Read)
                .AddRequirements(new HttpGetRequirement()));

            authorization.AddPolicy(AuthorizationPolicies.CustomerScoped, policy => ConfigureApiPolicy(policy)
                .AddRequirements(new CustomerScopedResourceRequirement()));

            authorization.FallbackPolicy = authorization.GetPolicy(AuthorizationPolicies.Write);
        });

        return services;
    }

    private static AuthorizationPolicyBuilder ConfigureApiPolicy(AuthorizationPolicyBuilder policy)
    {
        return policy
            .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser();
    }
}
