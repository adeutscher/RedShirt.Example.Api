namespace RedShirt.Example.Api.Constants;

/// <summary>
///     Permission values the API authorizes against. Realm roles are mapped to these
///     (see <see cref="Authorization.RolePermissionMap" />); controllers reference
///     <see cref="AuthorizationPolicies" />, not role names.
/// </summary>
public static class AuthorizationPermissions
{
    /// <summary>
    ///     JWT / <see cref="System.Security.Claims.Claim.Type" /> used for derived permission claims.
    /// </summary>
    public const string ClaimType = "permission";

    public const string Read = "api:read";

    public const string Write = "api:write";
}
