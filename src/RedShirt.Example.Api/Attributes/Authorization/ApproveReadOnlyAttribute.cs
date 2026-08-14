using Microsoft.AspNetCore.Authorization;
using RedShirt.Example.Api.Authorization.Constants;

namespace RedShirt.Example.Api.Attributes.Authorization;

/// <summary>
///     Marks an endpoint as approved for <see cref="BespokeAuthorizationPermissions.Read" />
///     on HTTP GET. Roles that also grant <see cref="BespokeAuthorizationPermissions.Write" />
///     still succeed because the role map includes read.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ApproveReadOnlyAttribute : AuthorizeAttribute
{
    public ApproveReadOnlyAttribute()
    {
        Policy = BespokeAuthorizationPolicies.ReadApproved;
    }
}