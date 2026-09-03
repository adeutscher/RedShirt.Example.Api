using RedShirt.Example.Api.Authorization.Constants;
using System.Collections.Frozen;

namespace RedShirt.Example.Api.Authorization;

/// <summary>
///     Maps IdP realm roles to API permissions. Role hierarchy belongs here (and in
///     Keycloak composites), not in authorization handlers.
/// </summary>
internal static class BespokeRolePermissionMap
{
    private static readonly FrozenSet<string> FullAccessPermissions = FrozenSet.ToFrozenSet(
        [
            BespokeAuthorizationPermissions.Read,
            BespokeAuthorizationPermissions.Write,
            BespokeAuthorizationPermissions.ProductRead,
            BespokeAuthorizationPermissions.ProductWrite,
            BespokeAuthorizationPermissions.OrderRead,
            BespokeAuthorizationPermissions.OrderWrite,
            BespokeAuthorizationPermissions.CustomerRead,
            BespokeAuthorizationPermissions.CustomerWrite,
            BespokeAuthorizationPermissions.UploadRead,
            BespokeAuthorizationPermissions.UploadWrite,
            BespokeAuthorizationPermissions.UploadPurge
        ],
        StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, FrozenSet<string>> PermissionsByRole =
        new Dictionary<string, FrozenSet<string>>(StringComparer.Ordinal)
        {
            [BespokeAuthorizationRoles.Analyst] = FrozenSet.ToFrozenSet(
                [BespokeAuthorizationPermissions.ProductRead],
                StringComparer.Ordinal),
            [BespokeAuthorizationRoles.Billing] = FrozenSet.ToFrozenSet(
                [BespokeAuthorizationPermissions.OrderRead, BespokeAuthorizationPermissions.OrderWrite],
                StringComparer.Ordinal),
            [BespokeAuthorizationRoles.UploadValidator] = FrozenSet.ToFrozenSet(
                [BespokeAuthorizationPermissions.UploadValidator],
                StringComparer.Ordinal),
            [BespokeAuthorizationRoles.Developer] = FullAccessPermissions,
            [BespokeAuthorizationRoles.Admin] = FrozenSet.ToFrozenSet(
                [.. FullAccessPermissions, BespokeAuthorizationPermissions.Unrestricted],
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