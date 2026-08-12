using Microsoft.AspNetCore.Authorization;

namespace RedShirt.Example.Api.Authorization.Requirements;

/// <summary>
///     Succeeds when the authorization resource is an HTTP GET.
/// </summary>
internal sealed class HttpGetRequirement : IAuthorizationRequirement;

internal sealed class HttpGetAuthorizationHandler : AuthorizationHandler<HttpGetRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        HttpGetRequirement requirement)
    {
        if (IsHttpGet(context.Resource))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    internal static bool IsHttpGet(object? resource)
    {
        var method = resource switch
        {
            HttpContext httpContext => httpContext.Request.Method,
            HttpRequest request => request.Method,
            _ => null
        };

        return method is not null && HttpMethods.IsGet(method);
    }
}