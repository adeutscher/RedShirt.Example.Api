using Microsoft.AspNetCore.Authorization;
using RedShirt.Example.Api.Authorization.Constants;

namespace RedShirt.Example.Api.Attributes.Authorization;

/// <summary>
///     Requires <see cref="BespokeAuthorizationPermissions.ProductWrite" />.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AuthorizeProductWriteAttribute : AuthorizeAttribute
{
    public AuthorizeProductWriteAttribute()
    {
        Policy = BespokeAuthorizationPolicies.ProductWrite;
    }
}
