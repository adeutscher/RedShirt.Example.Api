using RedShirt.Example.Api.Upload.Core.Models;
using System.Security.Claims;

namespace RedShirt.Example.Api.Authorization.ResourceScoping.Upload;

/// <summary>
///     Stub implementation for when authentication (and therefore authorization) is disabled.
/// </summary>
public sealed class StubUploadScopedResourceEnforcer : IUploadScopedResourceEnforcer
{
    public string? ConstrainSearchUploadedByUserId(ClaimsPrincipal user, string? requestedUploadedByUserId)
    {
        return requestedUploadedByUserId;
    }

    public Task EnsureCanAccessAsync(ClaimsPrincipal user, string uploadedByUserId, bool allowValidators = false)
    {
        return Task.CompletedTask;
    }

    public Task EnsureCanDownloadAsync(ClaimsPrincipal user, string uploadedByUserId, UploadState state)
    {
        return Task.CompletedTask;
    }
}