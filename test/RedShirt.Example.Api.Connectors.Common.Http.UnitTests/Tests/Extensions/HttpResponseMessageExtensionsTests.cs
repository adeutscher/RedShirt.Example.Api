using RedShirt.Example.Api.Connectors.Common.Http.Extensions;
using System.Net;
using System.Net.Http.Headers;

namespace RedShirt.Example.Api.Connectors.Common.Http.UnitTests.Tests.Extensions;

public class HttpResponseMessageExtensionsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    private static HttpResponseMessage CreateResponse(RetryConditionHeaderValue? retryAfter)
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = retryAfter;
        return response;
    }

    [Fact]
    public void GetRetryAfter_ReturnsDelta_WhenHeaderIsSeconds()
    {
        using var response = CreateResponse(new RetryConditionHeaderValue(TimeSpan.FromSeconds(7)));

        Assert.Equal(TimeSpan.FromSeconds(7), response.GetRetryAfter(Now));
    }

    [Fact]
    public void GetRetryAfter_ReturnsTimeUntilDate_WhenHeaderIsFutureDate()
    {
        using var response = CreateResponse(new RetryConditionHeaderValue(Now.AddSeconds(12)));

        Assert.Equal(TimeSpan.FromSeconds(12), response.GetRetryAfter(Now));
    }

    [Fact]
    public void GetRetryAfter_ReturnsZero_WhenHeaderIsPastDate()
    {
        using var response = CreateResponse(new RetryConditionHeaderValue(Now.AddSeconds(-12)));

        Assert.Equal(TimeSpan.Zero, response.GetRetryAfter(Now));
    }

    [Fact]
    public void GetRetryAfter_ReturnsNull_WhenHeaderIsMissing()
    {
        using var response = CreateResponse(null);

        Assert.Null(response.GetRetryAfter(Now));
    }

    [Fact]
    public void GetRetryAfter_ReturnsNull_WhenHeaderIsUnreadable()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.TryAddWithoutValidation("Retry-After", "soon");

        Assert.Null(response.GetRetryAfter(Now));
    }
}
