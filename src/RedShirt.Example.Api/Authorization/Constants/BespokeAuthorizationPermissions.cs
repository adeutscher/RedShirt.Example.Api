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

    /// <summary>
    ///     Bypass customer resource-scope checks (granted to <see cref="BespokeAuthorizationRoles.Admin" />).
    /// </summary>
    public const string Unrestricted = "api:unrestricted";

    public const string ProductRead = "product:read";

    public const string ProductWrite = "product:write";

    public const string OrderRead = "order:read";

    public const string OrderWrite = "order:write";

    public const string CustomerRead = "customer:read";

    public const string CustomerWrite = "customer:write";
}