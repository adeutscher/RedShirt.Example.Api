namespace RedShirt.Example.Api.Upload.Core.Models;

public enum UploadState
{
    Uploading,
    NotValidated,
    Verified,
    Rejected,
    Deleted,
    Stored
}
