namespace RedShirt.Example.Api.Common.Analyzers.Database.Abstractions.Attributes
{
    /// <summary>
    ///     Identifies a database key property
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class DbKeyAttribute : Attribute
    {
        public bool CanSearchInKeyRange { get; private set; }

        public DbKeyAttribute(bool canSearchInKeyRange = true)
        {
            CanSearchInKeyRange = canSearchInKeyRange;
        }
    }
}