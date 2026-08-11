using System.Collections.Frozen;
using RedShirt.Example.Api.Constants;

namespace RedShirt.Example.Api.Authorization;

/// <summary>
///     Maps IdP realm roles to API permissions. Role hierarchy belongs here (and in
///     Keycloak composites), not in authorization handlers.
/// </summary>
internal static class RolePermissionMap
{
    private static readonly FrozenDictionary<string, FrozenSet<string>> PermissionsByRole =
        new Dictionary<string, FrozenSet<string>>(StringComparer.Ordinal)
        {
            [AuthorizationRoles.ApiReadOnly] = FrozenSet.ToFrozenSet(
                [AuthorizationPermissions.Read],
                StringComparer.Ordinal),
            [AuthorizationRoles.ApiUser] = FrozenSet.ToFrozenSet(
                [AuthorizationPermissions.Read, AuthorizationPermissions.Write],
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
