using Microsoft.AspNetCore.Authorization;
using RedShirt.Example.Api.Authorization.Constants;

namespace RedShirt.Example.Api.Attributes.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AuthorizeUploadWriteAttribute : AuthorizeAttribute
{
    public AuthorizeUploadWriteAttribute()
    {
        Policy = BespokeAuthorizationPolicies.UploadWrite;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ApproveUploadReadOnlyAttribute : AuthorizeAttribute
{
    public ApproveUploadReadOnlyAttribute()
    {
        Policy = BespokeAuthorizationPolicies.UploadReadApproved;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AuthorizeUploadValidatorAttribute : AuthorizeAttribute
{
    public AuthorizeUploadValidatorAttribute()
    {
        Policy = BespokeAuthorizationPolicies.UploadValidator;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AuthorizeUploadReadOrValidatorAttribute : AuthorizeAttribute
{
    public AuthorizeUploadReadOrValidatorAttribute()
    {
        Policy = BespokeAuthorizationPolicies.UploadReadOrValidator;
    }
}
