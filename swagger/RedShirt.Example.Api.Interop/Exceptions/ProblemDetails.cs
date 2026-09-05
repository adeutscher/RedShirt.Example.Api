using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RedShirt.Example.Api.Interop.Exceptions;

// Hand-maintained DTO listed in nswag.json excludedTypeNames so NSwag does not emit a duplicate.
// Same cold-build constraint as Exceptions/SwaggerException.cs.
// EventStreamListener references this type before Client.generated.cs exists.
// It must live outside the generated file even though OpenAPI client methods also use it.
public class ProblemDetails
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public int? Status { get; set; }

    [JsonPropertyName("detail")]
    public string Detail { get; set; } = string.Empty;

    [JsonPropertyName("instance")]
    public string Instance { get; set; } = string.Empty;

    [JsonExtensionData]
    public IDictionary<string, object>? AdditionalProperties { get; set; }
}