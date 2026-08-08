namespace RedShirt.Example.Api.Common.Analyzers.Database.Abstractions.Attributes
{
    /// <summary>
    ///     Identifies a table for the code generator.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class DbTableAttribute : Attribute
    {
        public string TableName { get; private set; }
        public bool CanSearchInKey { get; private set; }

        public DbTableAttribute(string tableName, bool canSearchInKey = false)
        {
            TableName = tableName;
            CanSearchInKey = canSearchInKey;
        }
    }
}