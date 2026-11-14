using Microsoft.AspNetCore.Authorization;
using RedShirt.Example.Api.Authorization.Constants;

namespace RedShirt.Example.Api.Attributes.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ApproveBackgroundJobReadOnlyAttribute : AuthorizeAttribute
{
    public ApproveBackgroundJobReadOnlyAttribute()
    {
        Policy = BespokeAuthorizationPolicies.BackgroundJobReadApproved;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AuthorizeBackgroundJobWriteAttribute : AuthorizeAttribute
{
    public AuthorizeBackgroundJobWriteAttribute()
    {
        Policy = BespokeAuthorizationPolicies.BackgroundJobWrite;
    }
}
