using System.Text;

namespace RedShirt.Example.Api.DataStores.Analyzers.DapperMySql.Generation.Extensions;

public static class StringBuilderExtensions
{
    /// <summary>
    ///     Alternate phrasing of AppendLineWithIndent for when you prefer to put the indent number first
    /// </summary>
    /// <param name="stringBuilder"></param>
    /// <param name="indentCount"></param>
    /// <param name="content"></param>
    /// <returns></returns>
    public static StringBuilder AppendLineWithIndent(this StringBuilder stringBuilder, uint indentCount, string content)
    {
        return stringBuilder.AppendLineWithIndent(content, indentCount);
    }

    public static StringBuilder AppendLineWithIndent(this StringBuilder stringBuilder, string content,
        uint indentCount = 1)
    {
        for (var i = 0; i < indentCount; i++)
        {
            stringBuilder.Append("    ");
        }

        stringBuilder.AppendLine(content);
        return stringBuilder;
    }

    public static StringBuilder BlankLine(this StringBuilder stringBuilder)
    {
        stringBuilder.AppendLine();
        return stringBuilder;
    }

    public static StringBuilder CloseBracket(this StringBuilder stringBuilder, uint indentCount = 1)
    {
        return stringBuilder.AppendLineWithIndent("}", indentCount);
    }

    public static StringBuilder OpenBracket(this StringBuilder stringBuilder, uint indentCount = 1)
    {
        return stringBuilder.AppendLineWithIndent("{", indentCount);
    }
}