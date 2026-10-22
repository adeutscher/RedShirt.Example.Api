using NJsonSchema;

namespace RedShirt.Example.Api.OpenApi;

internal static class DecimalOpenApiSchema
{
    internal const string Format = "decimal";

    internal static void Apply(JsonSchema schema)
    {
        schema.Type = JsonObjectType.Number;
        schema.Format = Format;
    }

    internal static bool IsDecimalType(Type type)
    {
        return Nullable.GetUnderlyingType(type) == typeof(decimal) || type == typeof(decimal);
    }
}