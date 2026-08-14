using Microsoft.AspNetCore.Authorization;
using RedShirt.Example.Api.Authorization.Constants;

namespace RedShirt.Example.Api.Attributes.Authorization;

/// <summary>
///     Requires <see cref="BespokeAuthorizationPermissions.OrderWrite" />.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AuthorizeOrderWriteAttribute : AuthorizeAttribute
{
    public AuthorizeOrderWriteAttribute()
    {
        Policy = BespokeAuthorizationPolicies.OrderWrite;
    }
}