namespace RedShirt.Example.Api.Constants;

/// <summary>
///     JWT claim types used for resource-based authorization (in addition to roles/permissions).
/// </summary>
public static class AuthorizationClaims
{
    /// <summary>
    ///     Caller’s customer id (<see cref="Guid" />). Scoped callers may only access
    ///     orders whose <c>CustomerId</c> matches this claim.
    /// </summary>
    public const string CustomerId = "customer_id";
}
