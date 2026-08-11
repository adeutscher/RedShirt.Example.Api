using Microsoft.AspNetCore.Authorization;
using RedShirt.Example.Api.Constants;

namespace RedShirt.Example.Api.Authorization;

/// <summary>
///     Evaluates <see cref="ApiAccessRequirement" /> against realm roles and HTTP method.
/// </summary>
public sealed class ApiAccessAuthorizationHandler : AuthorizationHandler<ApiAccessRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ApiAccessRequirement requirement)
    {
        if (context.User.IsInRole(AuthorizationRoles.ApiUser))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (!requirement.ReadOnlyApprovedEndpoint
            || !context.User.IsInRole(AuthorizationRoles.ApiReadOnly))
        {
            return Task.CompletedTask;
        }

        var method = GetHttpMethod(context.Resource);
        if (method is not null && HttpMethods.IsGet(method))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static string? GetHttpMethod(object? resource)
    {
        return resource switch
        {
            HttpContext httpContext => httpContext.Request.Method,
            HttpRequest request => request.Method,
            _ => null
        };
    }
}
