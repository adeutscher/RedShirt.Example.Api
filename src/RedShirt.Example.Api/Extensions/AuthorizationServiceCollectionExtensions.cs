using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using RedShirt.Example.Api.Authorization;
using RedShirt.Example.Api.Authorization.Constants;
using RedShirt.Example.Api.Authorization.Requirements;
using RedShirt.Example.Api.Authorization.ResourceScoping.Customer;
using RedShirt.Example.Api.Authorization.ResourceScoping.Upload;

namespace RedShirt.Example.Api.Extensions;

internal static class AuthorizationServiceCollectionExtensions
{
    private static AuthorizationPolicyBuilder ConfigureApiPolicy(AuthorizationPolicyBuilder policy)
    {
        return policy
            .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser();
    }

    /// <summary>
    ///     Add services for when authorization is enabled.
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    internal static IServiceCollection AddApiAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddSingleton<IClaimsTransformation, BespokeRolePermissionClaimsTransformation>();
        services.AddSingleton<IAuthorizationHandler, HttpGetAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler, CustomerScopedResourceEnforcerHandler>();
        services.AddSingleton<IAuthorizationHandler, UploadScopedResourceEnforcerHandler>();
        services.AddSingleton<IAuthorizationHandler, UploadDownloadResourceEnforcerHandler>();
        services.AddSingleton<ICustomerScopedResourceEnforcer, CustomerScopedResourceEnforcer>();
        services.AddSingleton<IUploadScopedResourceEnforcer, UploadScopedResourceEnforcer>();

        services.AddAuthorization(authorization =>
        {
            authorization.AddPolicy(BespokeAuthorizationPolicies.Write, policy => ConfigureApiPolicy(policy)
                .RequireClaim(BespokeAuthorizationPermissions.ClaimType, BespokeAuthorizationPermissions.Write));

            authorization.AddPolicy(BespokeAuthorizationPolicies.ReadApproved, policy => ConfigureApiPolicy(policy)
                .RequireClaim(BespokeAuthorizationPermissions.ClaimType, BespokeAuthorizationPermissions.Read)
                .AddRequirements(new HttpGetRequirement()));

            authorization.AddPolicy(BespokeAuthorizationPolicies.ProductWrite, policy => ConfigureApiPolicy(policy)
                .RequireClaim(BespokeAuthorizationPermissions.ClaimType,
                    BespokeAuthorizationPermissions.ProductWrite));

            authorization.AddPolicy(BespokeAuthorizationPolicies.ProductReadApproved, policy =>
                ConfigureApiPolicy(policy)
                    .RequireClaim(BespokeAuthorizationPermissions.ClaimType,
                        BespokeAuthorizationPermissions.ProductRead)
                    .AddRequirements(new HttpGetRequirement()));

            authorization.AddPolicy(BespokeAuthorizationPolicies.OrderWrite, policy => ConfigureApiPolicy(policy)
                .RequireClaim(BespokeAuthorizationPermissions.ClaimType, BespokeAuthorizationPermissions.OrderWrite));

            authorization.AddPolicy(BespokeAuthorizationPolicies.OrderReadApproved, policy => ConfigureApiPolicy(policy)
                .RequireClaim(BespokeAuthorizationPermissions.ClaimType, BespokeAuthorizationPermissions.OrderRead)
                .AddRequirements(new HttpGetRequirement()));

            authorization.AddPolicy(BespokeAuthorizationPolicies.CustomerWrite, policy => ConfigureApiPolicy(policy)
                .RequireClaim(BespokeAuthorizationPermissions.ClaimType,
                    BespokeAuthorizationPermissions.CustomerWrite));

            authorization.AddPolicy(BespokeAuthorizationPolicies.CustomerReadApproved, policy =>
                ConfigureApiPolicy(policy)
                    .RequireClaim(BespokeAuthorizationPermissions.ClaimType,
                        BespokeAuthorizationPermissions.CustomerRead)
                    .AddRequirements(new HttpGetRequirement()));

            authorization.AddPolicy(BespokeAuthorizationPolicies.UploadWrite, policy => ConfigureApiPolicy(policy)
                .RequireClaim(BespokeAuthorizationPermissions.ClaimType, BespokeAuthorizationPermissions.UploadWrite));

            authorization.AddPolicy(BespokeAuthorizationPolicies.UploadReadApproved, policy =>
                ConfigureApiPolicy(policy)
                    .RequireClaim(BespokeAuthorizationPermissions.ClaimType,
                        BespokeAuthorizationPermissions.UploadRead)
                    .AddRequirements(new HttpGetRequirement()));

            authorization.AddPolicy(BespokeAuthorizationPolicies.UploadReadOrValidator, policy =>
                ConfigureApiPolicy(policy)
                    .RequireAssertion(context =>
                        context.User.HasClaim(BespokeAuthorizationPermissions.ClaimType,
                            BespokeAuthorizationPermissions.UploadRead)
                        || context.User.HasClaim(BespokeAuthorizationPermissions.ClaimType,
                            BespokeAuthorizationPermissions.UploadValidator))
                    .AddRequirements(new HttpGetRequirement()));

            authorization.AddPolicy(BespokeAuthorizationPolicies.UploadValidator, policy => ConfigureApiPolicy(policy)
                .RequireClaim(BespokeAuthorizationPermissions.ClaimType,
                    BespokeAuthorizationPermissions.UploadValidator));

            authorization.AddPolicy(BespokeAuthorizationPolicies.UploadDownloadApproved, policy =>
                ConfigureApiPolicy(policy)
                    .RequireAssertion(context =>
                        context.User.HasClaim(BespokeAuthorizationPermissions.ClaimType,
                            BespokeAuthorizationPermissions.UploadRead)
                        || context.User.HasClaim(BespokeAuthorizationPermissions.ClaimType,
                            BespokeAuthorizationPermissions.UploadValidator))
                    .AddRequirements(new HttpGetRequirement()));

            authorization.AddPolicy(BespokeAuthorizationPolicies.UploadScoped, policy => ConfigureApiPolicy(policy)
                .AddRequirements(new UploadScopedResourceRequirement()));

            authorization.AddPolicy(BespokeAuthorizationPolicies.UploadDownload, policy => ConfigureApiPolicy(policy)
                .AddRequirements(new UploadDownloadResourceRequirement()));

            authorization.AddPolicy(BespokeAuthorizationPolicies.UploadPurge, policy => ConfigureApiPolicy(policy)
                .RequireClaim(BespokeAuthorizationPermissions.ClaimType, BespokeAuthorizationPermissions.UploadPurge));

            authorization.AddPolicy(BespokeAuthorizationPolicies.CustomerScoped, policy => ConfigureApiPolicy(policy)
                .AddRequirements(new CustomerScopedResourceRequirement()));

            authorization.FallbackPolicy = authorization.GetPolicy(BespokeAuthorizationPolicies.Write);
        });

        return services;
    }

    /// <summary>
    ///     Consider adding stub implementations of inline resource enforcement such as scoped resource access.
    ///     Even if authentication (and therefore authorization) is disabled, endpoints will still demand these services.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configuration"></param>
    /// <returns></returns>
    internal static IServiceCollection ConsiderAddingStubAuthorizationScopedResourcePolicies(
        this IServiceCollection services,
        IConfigurationRoot configuration)
    {
        if (AuthenticationServiceCollectionExtensions.IsAuthenticationEnabled(configuration))
        {
            // If authentication is enabled, then no need for stubs
            return services;
        }

        return services
            .AddSingleton<ICustomerScopedResourceEnforcer, StubCustomerScopedResourceEnforcer>()
            .AddSingleton<IUploadScopedResourceEnforcer, StubUploadScopedResourceEnforcer>();
    }
}