using RedShirt.Example.Api.Authorization.Constants;
using System.Security.Claims;

namespace RedShirt.Example.Api.Authorization.Extensions;

/// <summary>
///     Resolves the authenticated caller's identity from JWT claims.
/// </summary>
internal static class ClaimsPrincipalExtensions
{
    public static bool TryGetUserId(this ClaimsPrincipal user, out string userId)
    {
        userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
                 ?? user.FindFirstValue(BespokeAuthorizationClaims.Sub)
                 ?? string.Empty;
        return !string.IsNullOrWhiteSpace(userId);
    }
}
