namespace RedShirt.Example.Api.Common.Analyzers.Database.Abstractions.Attributes
{
    /// <summary>
    ///     Marks the order in which search properties will be considered for filtering
    ///     Useful for filtering by indexed attributes first.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class FilterPriorityWeightAttribute : Attribute
    {
        public int Weight { get; private set; }

        public FilterPriorityWeightAttribute(int weight)
        {
            Weight = weight;
        }
    }
}