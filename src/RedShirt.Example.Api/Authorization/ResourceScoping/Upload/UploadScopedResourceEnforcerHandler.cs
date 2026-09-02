using Microsoft.AspNetCore.Authorization;
using RedShirt.Example.Api.Upload.Core.Models;

namespace RedShirt.Example.Api.Authorization.ResourceScoping.Upload;

internal sealed class UploadScopedResourceRequirement : IAuthorizationRequirement;

internal sealed record UploadScopedResource(string UploadedByUserId);

/// <summary>
///     Succeeds when the caller is unrestricted or the resource uploader matches the caller’s user id.
/// </summary>
internal sealed class UploadScopedResourceEnforcerHandler
    : AuthorizationHandler<UploadScopedResourceRequirement, UploadScopedResource>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        UploadScopedResourceRequirement requirement,
        UploadScopedResource resource)
    {
        if (UploadScope.IsUnrestricted(context.User))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (UploadScope.TryGetUserId(context.User, out var userId)
            && userId == resource.UploadedByUserId)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

internal sealed class UploadDownloadResourceRequirement : IAuthorizationRequirement;

internal sealed record UploadDownloadResource(string UploadedByUserId, UploadState State);

/// <summary>
///     Non-<see cref="UploadState.Stored" /> downloads: unrestricted or validator.
///     <see cref="UploadState.Stored" /> downloads: unrestricted or the original uploader.
/// </summary>
internal sealed class UploadDownloadResourceEnforcerHandler
    : AuthorizationHandler<UploadDownloadResourceRequirement, UploadDownloadResource>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        UploadDownloadResourceRequirement requirement,
        UploadDownloadResource resource)
    {
        if (UploadScope.IsUnrestricted(context.User))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (resource.State == UploadState.Stored)
        {
            if (UploadScope.TryGetUserId(context.User, out var userId)
                && userId == resource.UploadedByUserId)
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }

        if (UploadScope.IsValidator(context.User))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
