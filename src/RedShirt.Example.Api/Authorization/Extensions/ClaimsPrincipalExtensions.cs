using RedShirt.Example.Api.Authorization.Constants;
using RedShirt.Example.Api.Constants;
using System.Security.Claims;

namespace RedShirt.Example.Api.Authorization.Extensions;

/// <summary>
///     Resolves the authenticated caller's identity from JWT claims.
/// </summary>
internal static class ClaimsPrincipalExtensions
{
    /// <summary>
    ///     Caller identity for scoping per-caller state such as idempotency records.
    ///     Falls back to <see cref="CallerConstants.Anonymous" /> when there is no subject
    ///     (only reachable when authentication is disabled).
    /// </summary>
    public static string GetCallerIdOrAnonymous(this ClaimsPrincipal user)
    {
        return user.TryGetUserId(out var userId) ? userId : CallerConstants.Anonymous;
    }

    public static bool TryGetUserId(this ClaimsPrincipal user, out string userId)
    {
        userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
                 ?? user.FindFirstValue(BespokeAuthorizationClaims.Sub)
                 ?? string.Empty;
        return !string.IsNullOrWhiteSpace(userId);
    }
}