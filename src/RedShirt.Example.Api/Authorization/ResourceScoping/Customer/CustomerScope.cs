using RedShirt.Example.Api.Authorization.Constants;
using System.Security.Claims;

namespace RedShirt.Example.Api.Authorization.ResourceScoping.Customer;

/// <summary>
///     Shared rules for customer-scoped resources:
///     <see cref="BespokeAuthorizationPermissions.Unrestricted" /> bypasses scope;
///     otherwise the caller is limited to <see cref="BespokeAuthorizationClaims.CustomerId" />.
/// </summary>
internal static class CustomerScope
{
    public static bool IsUnrestricted(ClaimsPrincipal user)
    {
        return user.HasClaim(BespokeAuthorizationPermissions.ClaimType, BespokeAuthorizationPermissions.Unrestricted);
    }

    public static bool TryGetCustomerId(ClaimsPrincipal user, out Guid customerId)
    {
        var value = user.FindFirst(BespokeAuthorizationClaims.CustomerId)?.Value;
        return Guid.TryParse(value, out customerId);
    }
}