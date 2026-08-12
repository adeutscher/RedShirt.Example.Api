using RedShirt.Example.Api.Authorization.Constants;
using System.Collections.Frozen;

namespace RedShirt.Example.Api.Authorization;

/// <summary>
///     Maps IdP realm roles to API permissions. Role hierarchy belongs here (and in
///     Keycloak composites), not in authorization handlers.
/// </summary>
internal static class BespokeRolePermissionMap
{
    private static readonly FrozenDictionary<string, FrozenSet<string>> PermissionsByRole =
        new Dictionary<string, FrozenSet<string>>(StringComparer.Ordinal)
        {
            [BespokeAuthorizationRoles.ApiReadOnly] = FrozenSet.ToFrozenSet(
                [BespokeAuthorizationPermissions.Read],
                StringComparer.Ordinal),
            [BespokeAuthorizationRoles.ApiUser] = FrozenSet.ToFrozenSet(
                [BespokeAuthorizationPermissions.Read, BespokeAuthorizationPermissions.Write],
                StringComparer.Ordinal)
        }.ToFrozenDictionary(StringComparer.Ordinal);

    public static IReadOnlyCollection<string> GetPermissions(IEnumerable<string> roles)
    {
        var permissions = new HashSet<string>(StringComparer.Ordinal);

        foreach (var role in roles)
        {
            if (PermissionsByRole.TryGetValue(role, out var granted))
            {
                permissions.UnionWith(granted);
            }
        }

        return permissions;
    }
}