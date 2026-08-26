using Microsoft.AspNetCore.Authorization;
using RedShirt.Example.Api.Authorization.Constants;

namespace RedShirt.Example.Api.Attributes.Authorization;

/// <summary>
///     Requires <see cref="BespokeAuthorizationPermissions.CustomerWrite" />.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AuthorizeCustomerWriteAttribute : AuthorizeAttribute
{
    public AuthorizeCustomerWriteAttribute()
    {
        Policy = BespokeAuthorizationPolicies.CustomerWrite;
    }
}