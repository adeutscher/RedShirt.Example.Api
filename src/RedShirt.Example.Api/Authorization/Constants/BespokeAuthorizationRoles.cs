namespace RedShirt.Example.Api.Authorization.Constants;

/// <summary>
///     Realm role names expected in JWT access tokens (Keycloak <c>role</c> claims locally).
///     These are an identity-provider contract; endpoints authorize on
///     <see cref="BespokeAuthorizationPolicies" /> / <see cref="BespokeAuthorizationPermissions" />.
/// </summary>
public static class BespokeAuthorizationRoles
{
    /// <summary>
    ///     Full API access, including <see cref="BespokeAuthorizationPermissions.Unrestricted" />
    ///     (bypasses customer resource scope). Local Keycloak treats this as a composite that
    ///     includes <see cref="Developer" />; the API map also grants the full permission set.
    /// </summary>
    public const string Admin = "admin";

    /// <summary>
    ///     Full API access, still subject to customer resource scope on orders.
    /// </summary>
    public const string Developer = "developer";

    /// <summary>
    ///     Read-only access to Product (<see cref="BespokeAuthorizationPermissions.ProductRead" />).
    /// </summary>
    public const string Analyst = "analyst";

    /// <summary>
    ///     Read-write access to Order
    ///     (<see cref="BespokeAuthorizationPermissions.OrderRead" /> /
    ///     <see cref="BespokeAuthorizationPermissions.OrderWrite" />).
    /// </summary>
    public const string Billing = "billing";

    /// <summary>
    ///     Upload validation worker: GET upload by id, submit verdicts, and submit move reports.
    /// </summary>
    public const string UploadValidator = "upload-validator";
}