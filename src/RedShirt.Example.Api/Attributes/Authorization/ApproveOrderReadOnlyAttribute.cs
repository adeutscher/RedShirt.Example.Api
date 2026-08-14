using Microsoft.AspNetCore.Authorization;
using RedShirt.Example.Api.Authorization.Constants;

namespace RedShirt.Example.Api.Attributes.Authorization;

/// <summary>
///     Marks an Order endpoint as approved for
///     <see cref="BespokeAuthorizationPermissions.OrderRead" /> on HTTP GET.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ApproveOrderReadOnlyAttribute : AuthorizeAttribute
{
    public ApproveOrderReadOnlyAttribute()
    {
        Policy = BespokeAuthorizationPolicies.OrderReadApproved;
    }
}
