using System.Collections.Concurrent;
using System.Reflection;

namespace RedShirt.Example.Api.Common.Distributed.Continuation;

/// <summary>
///     Keeps a search continuation token from being replayed against a different entity or a different caller.
///     <para>
///         A continuation token maps to search parameters cached by the request that issued it. Without these checks,
///         a token issued by one entity's search (or one caller's search) could be passed to another search, which
///         would then run with the cached parameters instead of the ones the controller scoped to the current caller.
///     </para>
///     <list type="bullet">
///         <item>
///             <see cref="CacheKey" /> namespaces the cache entry by entity, so a token from one repository is a
///             cache miss in every other repository.
///         </item>
///         <item>
///             <see cref="IsCompatible{T}" /> rejects cached parameters that contradict the incoming request. Controllers
///             always set caller-scoping filters (for example the uploader or customer id), so another caller's token
///             fails this check.
///         </item>
///     </list>
///     Callers treat an incompatible token like an expired one and run the incoming request from the first page.
/// </summary>
public static class ContinuationTokenScope
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertiesByType = new();

    /// <summary>
    ///     Cache key for a continuation token issued by <paramref name="entity" />'s search.
    /// </summary>
    public static string CacheKey(string entity, Guid continuationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entity);
        return $"continuation:{entity}:{continuationToken}";
    }

    /// <summary>
    ///     Whether <paramref name="cached" /> search parameters may be continued by a request with
    ///     <paramref name="incoming" /> parameters.
    ///     <para>
    ///         A follow-up request may omit filters (they are carried by the token), but every filter it does set must
    ///         equal the cached value. A property counts as unset when it holds its type's default value (null for
    ///         reference and nullable types).
    ///     </para>
    /// </summary>
    /// <param name="incoming">Parameters of the request presenting the token, after caller scoping.</param>
    /// <param name="cached">Parameters stored when the token was issued.</param>
    /// <param name="ignoredProperties">
    ///     Properties that may differ between pages and are not filters (typically the page size).
    /// </param>
    public static bool IsCompatible<T>(T incoming, T cached, params string[] ignoredProperties) where T : class
    {
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentNullException.ThrowIfNull(cached);

        foreach (var property in PropertiesByType.GetOrAdd(typeof(T), ReadableProperties))
        {
            if (ignoredProperties.Contains(property.Name, StringComparer.Ordinal))
            {
                continue;
            }

            var incomingValue = property.GetValue(incoming);
            if (IsUnset(incomingValue, property.PropertyType))
            {
                continue;
            }

            if (!Equals(incomingValue, property.GetValue(cached)))
            {
                return false;
            }
        }

        return true;
    }

    private static PropertyInfo[] ReadableProperties(Type type)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanRead && property.GetIndexParameters().Length == 0)
            .ToArray();
    }

    private static bool IsUnset(object? value, Type propertyType)
    {
        if (value is null)
        {
            return true;
        }

        // Nullable<T> boxes a held value as T, so only a null box is unset (Guid.Empty in a Guid? is a real filter).
        return propertyType.IsValueType
               && Nullable.GetUnderlyingType(propertyType) is null
               && value.Equals(Activator.CreateInstance(propertyType));
    }
}
