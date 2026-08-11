using Microsoft.AspNetCore.Authorization;

namespace RedShirt.Example.Api.Authorization;

/// <summary>
///     Requires <see cref="Constants.AuthorizationRoles.ApiUser" />, or — when
///     <see cref="ReadOnlyApprovedEndpoint" /> is <see langword="true" /> —
///     <see cref="Constants.AuthorizationRoles.ApiReadOnly" /> on an HTTP GET.
/// </summary>
public sealed class ApiAccessRequirement : IAuthorizationRequirement
{
    public required bool ReadOnlyApprovedEndpoint { get; init; }
}
