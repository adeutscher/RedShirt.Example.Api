using System.Reflection;
using System.Text.Json;
using NJsonSchema.Generation;
using RedShirt.Example.Api.DataStores.Analyzers.Abstractions.Attributes;

namespace RedShirt.Example.Api.OpenApi;

internal sealed class StoredAsDecimalSchemaProcessor : ISchemaProcessor
{
    public void Process(SchemaProcessorContext context)
    {
        var type = context.ContextualType.Type;
        if (type is null || !type.IsClass)
        {
            return;
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetCustomAttribute<StoredAsDecimalAttribute>() is null)
            {
                continue;
            }

            var camelCaseName = JsonNamingPolicy.CamelCase.ConvertName(property.Name);
            if (context.Schema.Properties.TryGetValue(camelCaseName, out var propertySchema))
            {
                StoredAsDecimalOpenApiSchema.Apply(propertySchema);
            }
        }
    }
}
