using System.Text.Json.Serialization;

namespace RedShirt.Example.Api.Upload.Core.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UploadState
{
    Uploading,
    NotValidated,
    Verified,
    Rejected,
    Deleted,
    Stored
}