using System.Reflection;
using System.Text.Json;
using NJsonSchema.Generation;

namespace RedShirt.Example.Api.OpenApi;

internal sealed class DecimalFormatSchemaProcessor : ISchemaProcessor
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
            if (!DecimalOpenApiSchema.IsDecimalType(property.PropertyType))
            {
                continue;
            }

            var camelCaseName = JsonNamingPolicy.CamelCase.ConvertName(property.Name);
            if (context.Schema.Properties.TryGetValue(camelCaseName, out var propertySchema))
            {
                DecimalOpenApiSchema.Apply(propertySchema);
            }
        }
    }
}
