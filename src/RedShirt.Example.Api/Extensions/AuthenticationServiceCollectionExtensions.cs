using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NJsonSchema;
using NSwag;
using NSwag.Generation.Processors.Security;
using RedShirt.Example.Api.Configuration;

namespace RedShirt.Example.Api.Extensions;

internal static class AuthenticationServiceCollectionExtensions
{
    internal static IServiceCollection AddApiSwaggerDocument(this IServiceCollection services,
        IConfiguration configuration)
    {
        var authentication = configuration.GetSection(AuthenticationOptions.ConfigurationSectionName)
            .Get<AuthenticationOptions>();
        var authenticationEnabled = authentication is {DisableAuthentication: false};

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
                Description = "Paste a Keycloak access token (without the 'Bearer ' prefix)."
            });
            document.OperationProcessors.Add(new AspNetCoreOperationSecurityScopeProcessor("Bearer"));
        });
    }

    internal static IServiceCollection ConsiderAddingAuthentication(this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration.GetSection(AuthenticationOptions.ConfigurationSectionName)
            .Get<AuthenticationOptions>();

        if (options is null or {DisableAuthentication: true})
        {
            return services;
        }

        if (string.IsNullOrWhiteSpace(options.Authority))
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
                    ValidateLifetime = true
                };
            });

        services.AddAuthorization(authorization =>
        {
            authorization.FallbackPolicy = new AuthorizationPolicyBuilder()
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }
}

internal static class AuthenticationWebApplicationExtensions
{
    internal static WebApplication ConsiderUsingAuthentication(this WebApplication app)
    {
        var options = app.Services.GetService<IOptions<AuthenticationOptions>>()?.Value
                      ?? app.Configuration.GetSection(AuthenticationOptions.ConfigurationSectionName)
                          .Get<AuthenticationOptions>();

        if (options is null or {DisableAuthentication: true})
        {
            return app;
        }

        app.UseAuthentication();
        return app;
    }
}