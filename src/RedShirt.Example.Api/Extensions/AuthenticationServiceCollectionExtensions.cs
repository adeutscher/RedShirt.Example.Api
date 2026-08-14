using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NJsonSchema;
using NSwag;
using NSwag.Generation.Processors.Security;
using RedShirt.Example.Api.Configuration;

namespace RedShirt.Example.Api.Extensions;

internal static class AuthenticationServiceCollectionExtensions
{
    private static AuthenticationOptions? GetAuthenticationOptionsFromConfiguration(IConfiguration configuration)
    {
        return configuration.GetSection(AuthenticationOptions.ConfigurationSectionName)
            .Get<AuthenticationOptions>();
    }

    private static bool IsAuthenticationEnabled(AuthenticationOptions? options)
    {
        return options is not {DisableAuthentication: true};
    }

    internal static IServiceCollection AddApiSwaggerDocument(this IServiceCollection services,
        IConfigurationRoot configuration)
    {
        var authenticationEnabled = IsAuthenticationEnabled(configuration);

        return services.AddSwaggerDocument(document =>
        {
            document.Title = "RedShirt.Example.Api";
            document.SchemaSettings.SchemaType = SchemaType.OpenApi3;

            if (!authenticationEnabled)
            {
                return;
            }

            document.AddSecurity("Bearer", [], new OpenApiSecurityScheme
            {
                Type = OpenApiSecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Paste a JWT access token (without the 'Bearer ' prefix)."
            });
            document.OperationProcessors.Add(new AspNetCoreOperationSecurityScopeProcessor("Bearer"));
        });
    }

    internal static IServiceCollection ConsiderAddingAuthentication(this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = GetAuthenticationOptionsFromConfiguration(configuration);

        if (!IsAuthenticationEnabled(options))
        {
            return services;
        }

        if (string.IsNullOrWhiteSpace(options?.Authority))
        {
            throw new InvalidOperationException(
                "Authentication is enabled but Authentication:Authority is missing.");
        }

        services
            .Configure<AuthenticationOptions>(
                configuration.GetSection(AuthenticationOptions.ConfigurationSectionName))
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwt =>
            {
                jwt.Authority = options.Authority;
                jwt.Audience = options.Audience;
                jwt.RequireHttpsMetadata = options.EffectiveRequireHttpsMetadata;
                jwt.MapInboundClaims = false;

                if (!string.IsNullOrWhiteSpace(options.MetadataAddress))
                {
                    jwt.MetadataAddress = options.MetadataAddress;
                }

                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = options.Authority,
                    ValidateAudience = !string.IsNullOrWhiteSpace(options.Audience),
                    ValidAudience = options.Audience,
                    ValidateLifetime = true,
                    // Keycloak realm-role mapper emits multivalued "role" claims (see realm-example.json).
                    RoleClaimType = "role",
                    NameClaimType = "preferred_username"
                };
            });

        services.AddApiAuthorizationPolicies();

        return services;
    }

    internal static WebApplication ConsiderUsingAuthentication(this WebApplication app)
    {
        var options = app.Services.GetService<IOptions<AuthenticationOptions>>()?.Value
                      ?? GetAuthenticationOptionsFromConfiguration(app.Configuration);

        if (!IsAuthenticationEnabled(options))
        {
            return app;
        }

        app.UseAuthentication();
        return app;
    }

    internal static bool IsAuthenticationEnabled(IConfigurationRoot configuration)
    {
        var options = GetAuthenticationOptionsFromConfiguration(configuration);
        return IsAuthenticationEnabled(options);
    }
}