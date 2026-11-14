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

    public const string UploadRead = "upload:read";

    public const string UploadWrite = "upload:write";

    public const string UploadValidator = "upload:validator";

    /// <summary>
    ///     Hard-delete upload records and storage (granted to <see cref="BespokeAuthorizationRoles.Admin" /> and
    ///     <see cref="BespokeAuthorizationRoles.Developer" />).
    /// </summary>
    public const string UploadPurge = "upload:purge";

    public const string BackgroundJobRead = "background-job:read";

    /// <summary>
    ///     Administrative changes to background jobs, such as retrying a failed job.
    /// </summary>
    public const string BackgroundJobWrite = "background-job:write";

    /// <summary>
    ///     Lease background jobs and report their outcomes, as a worker outside the API would.
    /// </summary>
    public const string BackgroundJobWorker = "background-job:worker";

    // scaffold:permissions
}