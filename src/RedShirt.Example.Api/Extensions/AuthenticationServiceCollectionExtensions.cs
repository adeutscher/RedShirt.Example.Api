using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NJsonSchema;
using NSwag;
using NSwag.Generation.Processors.Security;
using RedShirt.Example.Api.Configuration;
using RedShirt.Example.Api.OpenApi;

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
            document.SchemaSettings.SchemaProcessors.Add(new DecimalFormatSchemaProcessor());
            document.OperationProcessors.Add(new DecimalFormatOperationProcessor());

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

        // Without an expected audience, any token the authority issues (for any client or API in the realm) would be
        // accepted here, so a missing value fails at startup instead of switching the check off.
        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            throw new InvalidOperationException(
                "Authentication is enabled but Authentication:Audience is missing. Set it to the aud claim that " +
                "tokens for this API carry.");
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
                    ValidateAudience = true,
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

    internal const string DisabledAuthenticationWarning =
        "Authentication is DISABLED (Authentication:DisableAuthentication=true). Endpoints without an authorization " +
        "attribute accept anonymous requests and resource scoping allows everything. Never use this outside local " +
        "development.";

    /// <summary>
    ///     Refuses to start when authentication is disabled outside the Development environment.
    ///     <para>
    ///         Disabling authentication is a local convenience (and how NSwag generates the OpenAPI document without an
    ///         identity provider). One stray environment variable must not be able to open a deployed API, so any other
    ///         environment fails fast at startup instead.
    ///     </para>
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     Authentication is disabled and the environment is not Development.
    /// </exception>
    internal static void EnsureDisabledAuthenticationIsAllowed(IConfiguration configuration,
        IHostEnvironment environment)
    {
        if (IsAuthenticationEnabled(GetAuthenticationOptionsFromConfiguration(configuration))
            || environment.IsDevelopment())
        {
            return;
        }

        throw new InvalidOperationException(
            "Authentication:DisableAuthentication is true, but the environment is " +
            $"'{environment.EnvironmentName}'. Authentication may only be disabled in the Development environment.");
    }

    /// <inheritdoc cref="EnsureDisabledAuthenticationIsAllowed(IConfiguration, IHostEnvironment)" />
    internal static WebApplicationBuilder EnsureDisabledAuthenticationIsAllowed(this WebApplicationBuilder builder)
    {
        EnsureDisabledAuthenticationIsAllowed(builder.Configuration, builder.Environment);
        return builder;
    }

    /// <summary>
    ///     Logs a warning at startup when authentication is disabled, so the mode is visible in the logs.
    /// </summary>
    internal static WebApplication WarnIfAuthenticationDisabled(this WebApplication app)
    {
        if (!IsAuthenticationEnabled(GetAuthenticationOptionsFromConfiguration(app.Configuration)))
        {
            app.Logger.LogWarning(DisabledAuthenticationWarning);
        }

        return app;
    }

    internal static bool IsAuthenticationEnabled(IConfigurationRoot configuration)
    {
        var options = GetAuthenticationOptionsFromConfiguration(configuration);
        return IsAuthenticationEnabled(options);
    }
}