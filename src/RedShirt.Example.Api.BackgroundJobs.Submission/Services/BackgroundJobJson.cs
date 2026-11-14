using System.Text.Json;

namespace RedShirt.Example.Api.BackgroundJobs.Submission.Services;

/// <summary>
///     Shared serializer settings for job parameters, so submitters and handlers agree on the format.
/// </summary>
public static class BackgroundJobJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
