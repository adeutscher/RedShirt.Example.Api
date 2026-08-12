using Microsoft.AspNetCore.Authorization;
using RedShirt.Example.Api.Authorization.Constants;
using RedShirt.Example.Api.Common.Exceptions.Responses;
using System.Security.Claims;

namespace RedShirt.Example.Api.Authorization.ResourceScoping.Customer;

public interface ICustomerScopedResourceAuthorizer
{
    /// <summary>
    ///     Restricts order search to the caller’s customer when they are not unrestricted.
    ///     Returns <see cref="Guid.Empty" /> when a scoped caller has no usable customer claim
    ///     or asked for a different customer (no rows, no leak).
    /// </summary>
    Guid? ConstrainSearchCustomerId(ClaimsPrincipal user, Guid? requestedCustomerId);

    /// <summary>
    ///     Throws <see cref="ResourceNotFoundException" /> when the caller may not see
    ///     the record (same status as a missing id, so existence is not leaked).
    /// </summary>
    Task EnsureCanAccessAsync(ClaimsPrincipal user, Guid customerId);
}

internal sealed class CustomerScopedResourceAuthorizer(IAuthorizationService authorization)
    : ICustomerScopedResourceAuthorizer
{
    public async Task EnsureCanAccessAsync(ClaimsPrincipal user, Guid customerId)
    {
        var result = await authorization.AuthorizeAsync(
            user,
            new CustomerScopedResource(customerId),
            BespokeAuthorizationPolicies.CustomerScoped);

        if (!result.Succeeded)
        {
            throw new ResourceNotFoundException();
        }
    }

    public Guid? ConstrainSearchCustomerId(ClaimsPrincipal user, Guid? requestedCustomerId)
    {
        if (CustomerScope.IsUnrestricted(user))
        {
            return requestedCustomerId;
        }

        // ReSharper disable once DuplicatedSequentialIfBodies
        if (!CustomerScope.TryGetCustomerId(user, out var scopedCustomerId))
        {
            return Guid.Empty;
        }

        if (requestedCustomerId is { } requested && requested != scopedCustomerId)
        {
            return Guid.Empty;
        }

        return scopedCustomerId;
    }
}