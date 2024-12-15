using NJsonSchema;
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
    .AddSwaggerDocument(s =>
    {
        s.Title = "RedShirt.Example.Api";
        s.SchemaSettings.SchemaType = SchemaType.OpenApi3;
    })
    .ConfigureApiServices(builder.Configuration)
    .AddControllersWithViews();

var app = builder.Build();

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
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    "default",
    "{controller=Home}/{action=Index}/{id?}");

app.Run();