using Microsoft.AspNetCore.Authorization;
using RedShirt.Example.Api.Authorization.Constants;

namespace RedShirt.Example.Api.Attributes.Authorization;

/// <summary>
///     Approved Customer read:
///     <see cref="BespokeAuthorizationPermissions.CustomerRead" /> on HTTP GET.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ApproveCustomerReadOnlyAttribute : AuthorizeAttribute
{
    public ApproveCustomerReadOnlyAttribute()
    {
        Policy = BespokeAuthorizationPolicies.CustomerReadApproved;
    }
}
