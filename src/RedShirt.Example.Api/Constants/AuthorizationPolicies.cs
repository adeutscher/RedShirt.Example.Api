namespace RedShirt.Example.Api.Constants;

/// <summary>
///     Named authorization policies registered when authentication is enabled.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>
    ///     Default / write access: requires <see cref="AuthorizationRoles.ApiUser" />.
    ///     Applied as the fallback policy when an endpoint does not specify another policy.
    /// </summary>
    public const string Write = "ApiWrite";

    /// <summary>
    ///     Approved read access: <see cref="AuthorizationRoles.ApiUser" /> or
    ///     <see cref="AuthorizationRoles.ApiReadOnly" /> on a GET. Used by
    ///     <see cref="Attributes.ApproveReadOnlyAttribute" />.
    /// </summary>
    public const string ReadApproved = "ApiReadApproved";
}
