using RedShirt.Example.Api.Attributes.Authorization;

namespace RedShirt.Example.Api.Authorization.Constants;

/// <summary>
///     Named authorization policies registered when authentication is enabled.
///     Policies require <see cref="BespokeAuthorizationPermissions" />, not IdP role names.
/// </summary>
public static class BespokeAuthorizationPolicies
{
    /// <summary>
    ///     Default / write access: requires <see cref="BespokeAuthorizationPermissions.Write" />.
    ///     Applied as the fallback policy when an endpoint does not specify another policy.
    /// </summary>
    public const string Write = "ApiWrite";

    /// <summary>
    ///     Approved read access: <see cref="BespokeAuthorizationPermissions.Read" /> on an HTTP GET.
    ///     Used by <see cref="ApproveReadOnlyAttribute" />.
    /// </summary>
    public const string ReadApproved = "ApiReadApproved";

    /// <summary>
    ///     Product write access: requires <see cref="BespokeAuthorizationPermissions.ProductWrite" />.
    /// </summary>
    public const string ProductWrite = "ProductWrite";

    /// <summary>
    ///     Approved Product read: <see cref="BespokeAuthorizationPermissions.ProductRead" /> on an HTTP GET.
    /// </summary>
    public const string ProductReadApproved = "ProductReadApproved";

    /// <summary>
    ///     Order write access: requires <see cref="BespokeAuthorizationPermissions.OrderWrite" />.
    /// </summary>
    public const string OrderWrite = "OrderWrite";

    /// <summary>
    ///     Approved Order read: <see cref="BespokeAuthorizationPermissions.OrderRead" /> on an HTTP GET.
    /// </summary>
    public const string OrderReadApproved = "OrderReadApproved";

    /// <summary>
    ///     Customer write access: requires <see cref="BespokeAuthorizationPermissions.CustomerWrite" />.
    /// </summary>
    public const string CustomerWrite = "CustomerWrite";

    /// <summary>
    ///     Approved Customer read: <see cref="BespokeAuthorizationPermissions.CustomerRead" /> on an HTTP GET.
    /// </summary>
    public const string CustomerReadApproved = "CustomerReadApproved";

    /// <summary>
    ///     Upload write access: requires <see cref="BespokeAuthorizationPermissions.UploadWrite" />.
    /// </summary>
    public const string UploadWrite = "UploadWrite";

    /// <summary>
    ///     Approved Upload read: <see cref="BespokeAuthorizationPermissions.UploadRead" /> on an HTTP GET.
    /// </summary>
    public const string UploadReadApproved = "UploadReadApproved";

    /// <summary>
    ///     Upload validator worker access for verdict and move-report endpoints.
    /// </summary>
    public const string UploadValidator = "UploadValidator";

    /// <summary>
    ///     Upload summary read for callers with read or validator permissions.
    /// </summary>
    public const string UploadReadOrValidator = "UploadReadOrValidator";

    /// <summary>
    ///     Resource-based access to a customer-scoped record (for example an order).
    ///     Invoked with an explicit resource via <c>IAuthorizationService</c>, not as an
    ///     endpoint attribute (the resource is not available until the row is loaded).
    /// </summary>
    public const string CustomerScoped = "ApiCustomerScoped";
}