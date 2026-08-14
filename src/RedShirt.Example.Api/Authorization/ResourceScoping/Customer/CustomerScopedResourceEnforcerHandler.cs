using Microsoft.AspNetCore.Authorization;

namespace RedShirt.Example.Api.Authorization.ResourceScoping.Customer;

internal sealed class CustomerScopedResourceRequirement : IAuthorizationRequirement;

internal sealed record CustomerScopedResource(Guid CustomerId);

/// <summary>
///     Succeeds when the caller is unrestricted (any customer) or the resource
///     <see cref="CustomerScopedResource.CustomerId" /> matches the caller’s customer claim.
/// </summary>
internal sealed class CustomerScopedResourceEnforcerHandler
    : AuthorizationHandler<CustomerScopedResourceRequirement, CustomerScopedResource>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CustomerScopedResourceRequirement requirement,
        CustomerScopedResource resource)
    {
        if (CustomerScope.IsUnrestricted(context.User))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (CustomerScope.TryGetCustomerId(context.User, out var customerId)
            && customerId == resource.CustomerId)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}