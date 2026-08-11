using Microsoft.AspNetCore.Authorization;
using RedShirt.Example.Api.Constants;

namespace RedShirt.Example.Api.Attributes;

/// <summary>
///     Marks an endpoint as approved for the <see cref="AuthorizationRoles.ApiReadOnly" /> role.
///     Callers with that role may invoke it only via HTTP GET; callers with
///     <see cref="AuthorizationRoles.ApiUser" /> retain full access.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ApproveReadOnlyAttribute : AuthorizeAttribute
{
    public ApproveReadOnlyAttribute()
    {
        Policy = AuthorizationPolicies.ReadApproved;
    }
}
