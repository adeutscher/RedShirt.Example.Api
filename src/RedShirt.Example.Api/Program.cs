using RedShirt.Example.Api.Common.RateLimiting.Extensions;
using RedShirt.Example.Api.Extensions;

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

// Add services to the container.
builder.Services
    .AddApiSwaggerDocument(builder.Configuration)
    .ConfigureApiServices(builder.Configuration)
    .AddControllersWithViews();

var app = builder.Build();

app
    // If we add a path base, then it must be the first middleware that we set.
    .ConsiderAddingPathBase()
    .UseExceptionHandler();

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
app.UseStaticFiles();

app.UseRouting();
app.ConsiderUsingAuthentication();
app.UseAuthorization();

if (Environment.GetEnvironmentVariable("NSWAG_RUN") != "1")
{
    // Note: Must add after UseRouting is declared.
    app.ConsiderAddingRateLimitParts();
}

app
    .MapControllerRoute(
        "default",
        "{controller=Home}/{action=Index}/{id?}")
    .ConsiderRequiringRateLimiting(app.Configuration);

await app.RunAsync();