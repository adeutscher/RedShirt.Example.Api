namespace RedShirt.Example.Api.DataStores.Common.DapperMySql.Utility;

public static class SqlQueryHelper
{
    /// <summary>
    ///     The escape character that <see cref="EncodeTermForLike" /> uses.
    ///     <para>
    ///         Not a backslash: MariaDB treats backslashes in string literals differently when <c>NO_BACKSLASH_ESCAPES</c> is
    ///         set, and SQLite has no default LIKE escape character at all.
    ///     </para>
    /// </summary>
    public const char LikeEscapeCharacter = '!';

    /// <summary>
    ///     Append after a <c>LIKE @parameter</c> whose value comes from <see cref="EncodeTermForLike" />.
    /// </summary>
    public const string LikeEscapeClause = "ESCAPE '!'";

    /// <summary>
    ///     Builds a LIKE pattern that matches values containing <paramref name="term" /> literally. The <c>%</c> and
    ///     <c>_</c> wildcards (and the escape character itself) in the term are escaped, so callers cannot widen the
    ///     match. Use it with <see cref="LikeEscapeClause" />, for example <c>`Name` LIKE @name ESCAPE '!'</c>.
    /// </summary>
    public static string EncodeTermForLike(string term)
    {
        ArgumentNullException.ThrowIfNull(term);

        var escaped = term
            .Replace(LikeEscapeCharacter.ToString(), $"{LikeEscapeCharacter}{LikeEscapeCharacter}")
            .Replace("%", $"{LikeEscapeCharacter}%")
            .Replace("_", $"{LikeEscapeCharacter}_");
        return $"%{escaped}%";
    }
}