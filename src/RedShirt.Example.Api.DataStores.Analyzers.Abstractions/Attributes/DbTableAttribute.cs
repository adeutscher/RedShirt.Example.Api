namespace RedShirt.Example.Api.DataStores.Analyzers.Abstractions.Attributes;

/// <summary>
///     Identifies a table for the code generator.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class DbTableAttribute : Attribute
{
    public string TableName { get; private set; }
    public string ConnectionStringName { get; private set; }
    public bool CanSearchInKey { get; private set; }

    public DbTableAttribute(string tableName, string connectionStringName = null, bool canSearchInKey = false)
    {
        TableName = tableName;
        ConnectionStringName = connectionStringName;
        CanSearchInKey = canSearchInKey;
    }
}