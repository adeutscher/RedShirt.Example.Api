namespace RedShirt.Example.Api.Extensions;

internal static class WebApplicationExtensions
{
    public static WebApplication ConsiderAddingPathBase(this WebApplication app)
    {
        var pathBase = app.Configuration.GetSection("API").Get<ConfigurationModel>()?.PathBase;

        if (!string.IsNullOrWhiteSpace(pathBase))
        {
            app.UsePathBase(pathBase);
        }

        return app;
    }

    private sealed class ConfigurationModel
    {
        public required string? PathBase { get; init; }
    }
}