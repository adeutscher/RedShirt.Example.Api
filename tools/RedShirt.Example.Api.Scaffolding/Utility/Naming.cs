using System.Text.RegularExpressions;

namespace RedShirt.Example.Api.Scaffolding.Utility;

internal static partial class Naming
{
    [GeneratedRegex("(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])")]
    private static partial Regex WordBoundary();

    /// <summary>
    ///     "BeansWidget" -> "beans-widget"
    /// </summary>
    public static string ToKebabCase(string pascal)
    {
        return WordBoundary().Replace(pascal, "-").ToLowerInvariant();
    }

    /// <summary>
    ///     "BeansWidget" -> "beansWidget"
    /// </summary>
    public static string ToCamelCase(string pascal)
    {
        return pascal.Length == 0 ? pascal : char.ToLowerInvariant(pascal[0]) + pascal[1..];
    }

    /// <summary>
    ///     Naive English plural, good enough for a default route ("order" -> "orders", "category" -> "categories").
    /// </summary>
    public static string Pluralize(string word)
    {
        if (word.EndsWith('y') && word.Length > 1 && !"aeiou".Contains(word[^2]))
        {
            return word[..^1] + "ies";
        }

        if (word.EndsWith('s') || word.EndsWith('x') || word.EndsWith("ch", StringComparison.Ordinal) ||
            word.EndsWith("sh", StringComparison.Ordinal))
        {
            return word + "es";
        }

        return word + "s";
    }

    /// <summary>
    ///     "BeansWidget" -> "beans-widgets"
    /// </summary>
    public static string DefaultRoute(string pascal)
    {
        return Pluralize(ToKebabCase(pascal));
    }
}
