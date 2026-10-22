using NJsonSchema;

namespace RedShirt.Example.Api.OpenApi;

internal static class StoredAsDecimalOpenApiSchema
{
    internal const string Format = "decimal";

    internal static void Apply(JsonSchema schema)
    {
        schema.Type = JsonObjectType.Number;
        schema.Format = Format;
    }
}
