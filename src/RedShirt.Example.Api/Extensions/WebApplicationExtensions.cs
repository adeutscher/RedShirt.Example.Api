namespace RedShirt.Example.Api.Extensions;

internal static class WebApplicationExtensions
{
    public static WebApplication ConsiderAddingPathBase(this WebApplication app)
    {
        var basePath = app.Configuration.GetSection("API").Get<ConfigurationModel>()?.BasePath;

        if (!string.IsNullOrWhiteSpace(basePath))
        {
            app.UsePathBase(basePath);
        }

        return app;
    }

    private sealed class ConfigurationModel
    {
        public required string? BasePath { get; init; }
    }
}