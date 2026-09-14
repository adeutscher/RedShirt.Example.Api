using Microsoft.AspNetCore.Http;
using RedShirt.Example.Api.Extensions;

namespace RedShirt.Example.Api.UnitTests.Tests.Extensions;

public class HttpRequestExceptionsTests
{
    private static HttpRequest CreateRequest(
        string scheme,
        string host,
        int? port = null,
        string pathBase = "")
    {
        var context = new DefaultHttpContext
        {
            Request =
            {
                Scheme = scheme,
                Host = port is null ? new HostString(host) : new HostString(host, port.Value),
                PathBase = new PathString(pathBase),
            },
        };

        return context.Request;
    }

    [Fact]
    public void GetPublicBaseUri_ThrowsWhenRequestIsNull()
    {
        HttpRequest? request = null;

        Assert.Throws<ArgumentNullException>(() => request!.GetPublicBaseUri());
    }

    [Theory]
    [InlineData("https", "example.com", null, "", "https://example.com/")]
    [InlineData("http", "example.com", null, "", "http://example.com/")]
    [InlineData("https", "example.com", 443, "", "https://example.com/")]
    [InlineData("http", "example.com", 80, "", "http://example.com/")]
    [InlineData("https", "example.com", 8443, "", "https://example.com:8443/")]
    [InlineData("http", "example.com", 8080, "", "http://example.com:8080/")]
    [InlineData("https", "example.com", null, "/api", "https://example.com/api")]
    public void GetPublicBaseUri_BuildsUriFromSchemeHostPortAndPathBase(
        string scheme,
        string host,
        int? port,
        string pathBase,
        string expected)
    {
        var uri = CreateRequest(scheme, host, port, pathBase).GetPublicBaseUri();

        Assert.Equal(expected, uri.ToString());
    }
}
