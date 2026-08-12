namespace RedShirt.Example.Api.Authorization.Constants;

/// <summary>
///     Permission values the API authorizes against. Realm roles are mapped to these
///     (see <see cref="BespokeRolePermissionMap" />); controllers reference
///     <see cref="BespokeAuthorizationPolicies" />, not role names.
/// </summary>
public static class BespokeAuthorizationPermissions
{
    /// <summary>
    ///     JWT / <see cref="System.Security.Claims.Claim.Type" /> used for derived permission claims.
    /// </summary>
    public const string ClaimType = "permission";

    public const string Read = "api:read";

    public const string Write = "api:write";
}