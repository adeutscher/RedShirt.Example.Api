using Microsoft.AspNetCore.Authorization;
using RedShirt.Example.Api.Authorization.Constants;
using RedShirt.Example.Api.Authorization.Extensions;
using RedShirt.Example.Api.Common.Exceptions.Responses;
using RedShirt.Example.Api.Upload.Core.Models;
using System.Security.Claims;

namespace RedShirt.Example.Api.Authorization.ResourceScoping.Upload;

public interface IUploadScopedResourceEnforcer
{
    /// <summary>
    ///     Restricts upload search to the caller’s user id when they are not unrestricted.
    ///     Returns <see cref="UploadScope.NoAccessSentinel" /> when a scoped caller has no usable
    ///     user id or asked for a different uploader (no rows, no leak).
    /// </summary>
    string? ConstrainSearchUploadedByUserId(ClaimsPrincipal user, string? requestedUploadedByUserId);

    /// <summary>
    ///     Throws <see cref="ResourceNotFoundException" /> when the caller may not access the upload.
    /// </summary>
    Task EnsureCanAccessAsync(ClaimsPrincipal user, string uploadedByUserId, bool allowValidators = false);

    /// <summary>
    ///     Throws <see cref="ResourceNotFoundException" /> when the caller may not obtain a download link.
    /// </summary>
    Task EnsureCanDownloadAsync(ClaimsPrincipal user, string uploadedByUserId, UploadState state);
}

internal sealed class UploadScopedResourceEnforcer(IAuthorizationService authorization)
    : IUploadScopedResourceEnforcer
{
    public async Task EnsureCanAccessAsync(ClaimsPrincipal user, string uploadedByUserId, bool allowValidators = false)
    {
        var result = await authorization.AuthorizeAsync(
            user,
            new UploadScopedResource(uploadedByUserId, allowValidators),
            BespokeAuthorizationPolicies.UploadScoped);

        if (!result.Succeeded)
        {
            throw new ResourceNotFoundException();
        }
    }

    public async Task EnsureCanDownloadAsync(ClaimsPrincipal user, string uploadedByUserId, UploadState state)
    {
        var result = await authorization.AuthorizeAsync(
            user,
            new UploadDownloadResource(uploadedByUserId, state),
            BespokeAuthorizationPolicies.UploadDownload);

        if (!result.Succeeded)
        {
            throw new ResourceNotFoundException();
        }
    }

    public string? ConstrainSearchUploadedByUserId(ClaimsPrincipal user, string? requestedUploadedByUserId)
    {
        if (UploadScope.IsUnrestricted(user))
        {
            return requestedUploadedByUserId;
        }

        // ReSharper disable once DuplicatedSequentialIfBodies
        if (!user.TryGetUserId(out var scopedUserId))
        {
            return UploadScope.NoAccessSentinel;
        }

        if (!string.IsNullOrWhiteSpace(requestedUploadedByUserId)
            && !string.Equals(requestedUploadedByUserId, scopedUserId, StringComparison.Ordinal))
        {
            return UploadScope.NoAccessSentinel;
        }

        return scopedUserId;
    }
}