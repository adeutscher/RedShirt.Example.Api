namespace RedShirt.Example.Api.Constants;

/// <summary>
///     Named authorization policies registered when authentication is enabled.
///     Policies require <see cref="AuthorizationPermissions" />, not IdP role names.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>
    ///     Default / write access: requires <see cref="AuthorizationPermissions.Write" />.
    ///     Applied as the fallback policy when an endpoint does not specify another policy.
    /// </summary>
    public const string Write = "ApiWrite";

    /// <summary>
    ///     Approved read access: <see cref="AuthorizationPermissions.Read" /> on an HTTP GET.
    ///     Used by <see cref="Attributes.ApproveReadOnlyAttribute" />.
    /// </summary>
    public const string ReadApproved = "ApiReadApproved";

    /// <summary>
    ///     Resource-based access to a customer-scoped record (for example an order).
    ///     Invoked with an explicit resource via <c>IAuthorizationService</c>, not as an
    ///     endpoint attribute (the resource is not available until the row is loaded).
    /// </summary>
    public const string CustomerScoped = "ApiCustomerScoped";
}
