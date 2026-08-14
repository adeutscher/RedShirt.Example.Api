using Microsoft.AspNetCore.Authorization;
using RedShirt.Example.Api.Authorization.Constants;

namespace RedShirt.Example.Api.Attributes.Authorization;

/// <summary>
///     Marks a Product endpoint as approved for
///     <see cref="BespokeAuthorizationPermissions.ProductRead" /> on HTTP GET.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ApproveProductReadOnlyAttribute : AuthorizeAttribute
{
    public ApproveProductReadOnlyAttribute()
    {
        Policy = BespokeAuthorizationPolicies.ProductReadApproved;
    }
}
