using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.Extensions;
using System.Net;

namespace RedShirt.Example.Api.UnitTests.Tests.Extensions;

public class SecurityHeadersExtensionsTests
{
    private static async Task<HttpResponseMessage> SendAsync(string path)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddProblemDetails();
        builder.Services.AddRouting();

        var app = builder.Build();
        // Same order as Program.cs.
        app.UseSecurityHeaders();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseRouting();
        app.MapGet("/ok", () => Results.Ok(new {value = 1}));
        app.MapGet("/throws", IResult () => throw new InvalidOperationException("boom"));
        app.MapGet("/cached", (HttpResponse response) =>
        {
            response.Headers.CacheControl = "public, max-age=60";
            return Results.Ok();
        });
        app.MapGet("/swagger/index.html", () => Results.Content("<html></html>", "text/html"));

        await app.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            using var client = app.GetTestClient();
            return await client.GetAsync(path, TestContext.Current.CancellationToken);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    private static string? Header(HttpResponseMessage response, string name)
    {
        return response.Headers.TryGetValues(name, out var values)
               || response.Content.Headers.TryGetValues(name, out values)
            ? string.Join(", ", values)
            : null;
    }

    [Theory]
    [InlineData("/ok", HttpStatusCode.OK)]
    [InlineData("/throws", HttpStatusCode.InternalServerError)]
    [InlineData("/missing", HttpStatusCode.NotFound)]
    public async Task UseSecurityHeaders_AddsHeaders_IncludingOnErrorResponses(string path, HttpStatusCode expected)
    {
        using var response = await SendAsync(path);

        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
        Assert.Equal("DENY", Header(response, "X-Frame-Options"));
        Assert.Equal("no-referrer", Header(response, "Referrer-Policy"));
        Assert.Equal(SecurityHeadersExtensions.ApiContentSecurityPolicy, Header(response, "Content-Security-Policy"));
        // The exception handler sets its own "no-cache, no-store", which is kept.
        Assert.Contains("no-store", Header(response, "Cache-Control"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UseSecurityHeaders_KeepsCachePolicySetByTheEndpoint()
    {
        using var response = await SendAsync("/cached");

        Assert.Equal("public, max-age=60", Header(response, "Cache-Control"));
        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
    }

    [Fact]
    public async Task UseSecurityHeaders_SkipsContentSecurityPolicyForSwaggerUi()
    {
        using var response = await SendAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(Header(response, "Content-Security-Policy"));
        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
        Assert.Equal("DENY", Header(response, "X-Frame-Options"));
    }
}