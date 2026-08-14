using System.Security.Claims;

namespace RedShirt.Example.Api.Authorization.ResourceScoping.Customer;

/// <summary>
///     Stub implementation of scoped-resource enforcer for when authentication (and therefore authorization) is disabled.
/// </summary>
public class StubCustomerScopedResourceEnforcer : ICustomerScopedResourceEnforcer
{
    public Guid? ConstrainSearchCustomerId(ClaimsPrincipal user, Guid? requestedCustomerId)
    {
        return requestedCustomerId;
    }

    public Task EnsureCanAccessAsync(ClaimsPrincipal user, Guid customerId)
    {
        // Pass
        return Task.CompletedTask;
    }
}