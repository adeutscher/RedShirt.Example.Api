namespace RedShirt.Example.Api.Authorization.Constants;

/// <summary>
///     JWT claim types used for resource-based authorization (in addition to roles/permissions).
/// </summary>
public static class BespokeAuthorizationClaims
{
    /// <summary>
    ///     Caller’s customer id (<see cref="Guid" />). Scoped callers (everyone except
    ///     <see cref="BespokeAuthorizationRoles.Admin" /> / <see cref="BespokeAuthorizationPermissions.Unrestricted" />)
    ///     may only access orders whose <c>CustomerId</c> matches this claim.
    /// </summary>
    public const string CustomerId = "customer_id";

    /// <summary>
    ///     Standard OIDC subject claim; also surfaced as <see cref="System.Security.Claims.ClaimTypes.NameIdentifier" />.
    /// </summary>
    public const string Sub = "sub";
}