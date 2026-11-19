using RedShirt.Example.Api.Common.RateLimiting.Extensions;
using RedShirt.Example.Api.Extensions;
using RedShirt.Example.Api.Upload.Core.Configuration;

/*
 * citing sources:
 *  * Attempting NSwag packages until something started spitting out Swagger/OpenAPI-related
 *      extensions on IServiceCollection
 *  * https://github.com/RicoSuter/NSwag/issues/2409
 */

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddConfiguration(new ConfigurationBuilder()
        .AddEnvironmentVariablesWithSegmentSupport()
        .Build()
    );

// Fail fast if authentication is disabled anywhere but Development.
builder.EnsureDisabledAuthenticationIsAllowed();

if (builder.Configuration.GetSection(UploadOptions.ConfigurationSectionName).Get<UploadOptions>() is
    {
        MaxUploadSizeBytes: > 0
    } uploadOptions)
{
    /*
     * Attempting to set a maximum limit through Kestrel settings.
     * In practice in local testing, the server doesn't seem to enforce this
     * until after significant work has been done, so implementations should
     * also enforce their own size based on Content-Length header.
     */
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Limits.MaxRequestBodySize = uploadOptions.MaxUploadSizeBytes.Value;
    });
}

// Add services to the container.
builder.Services
    .AddApiSwaggerDocument(builder.Configuration)
    .ConfigureApiServices(builder.Configuration)
    .ConsiderConfiguringForwardingHeaders(builder.Configuration)
    .AddControllers();

var app = builder.Build();
app.WarnIfAuthenticationDisabled();
app.WarnAboutExceptionMessageExposure();

// First, so every response (including exception handler and status code page responses) gets the headers.
app.UseSecurityHeaders();

app
    .ConsiderUsingForwardedHeaders()
    .UseExceptionHandler()
    // Empty error responses (for example a route constraint miss) get a ProblemDetails body.
    .UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app
        .UseOpenApi()
        .UseSwaggerUi(u =>
        {
            u.Path = "/swagger";
            u.DocumentTitle = "RedShirt.Example.Api Swagger";
        });
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();
app.ConsiderUsingAuthentication();

if (Environment.GetEnvironmentVariable("NSWAG_RUN") != "1")
{
    /*
     * Order matters:
     *  * After UseRouting, so endpoint rate-limit metadata ([EnableRateLimiting]) is visible.
     *  * After authentication, so the partition key can use the validated JWT subject.
     *  * Before UseAuthorization, so requests that fail authorization (missing, invalid, or under-privileged tokens)
     *      are still counted. Unauthenticated callers are partitioned by remote IP.
     */
    app.ConsiderAddingRateLimitParts();
}

// Always set UseAuthorization because requirements aren't guaranteed to be based on authenticated identity.
app.UseAuthorization();

// Every controller uses attribute routing ([Route]), so there is no conventional route.
app
    .MapControllers()
    .ConsiderRequiringRateLimiting(app.Configuration);

await app.RunAsync();

public partial class Program;