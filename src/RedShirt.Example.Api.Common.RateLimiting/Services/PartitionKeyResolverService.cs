using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace RedShirt.Example.Api.Common.RateLimiting.Services;

internal interface IPartitionKeyResolverService
{
    string ResolvePartitionKey(HttpContext httpContext);
}

internal class PartitionKeyResolverService : IPartitionKeyResolverService
{
    public string ResolvePartitionKey(HttpContext httpContext)
    {
        var subject = httpContext.User.FindFirstValue("sub")
                      ?? httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!string.IsNullOrWhiteSpace(subject))
        {
            return $"user:{subject}";
        }

        var ip = httpContext.Connection.RemoteIpAddress?.ToString();
        return string.IsNullOrWhiteSpace(ip) ? "anonymous" : $"ip:{ip}";
    }
}