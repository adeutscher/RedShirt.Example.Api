using Microsoft.AspNetCore.Authorization;
using RedShirt.Example.Api.Authorization.Constants;
using RedShirt.Example.Api.Authorization.ResourceScoping.Upload;
using RedShirt.Example.Api.Upload.Core.Models;
using System.Security.Claims;

namespace RedShirt.Example.Api.UnitTests.Tests.Authorization;

public class UploadScopedResourceEnforcerHandlerTests
{
    private const string UploaderUserId = "uploader-user-id";

    private static AuthorizationHandlerContext CreateContext(
        ClaimsPrincipal user,
        bool allowValidators = false)
    {
        return new AuthorizationHandlerContext(
            [new UploadScopedResourceRequirement()],
            user,
            new UploadScopedResource(UploaderUserId, allowValidators));
    }

    private static ClaimsPrincipal Principal(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "test", "preferred_username", "role");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task HandleAsync_MatchingUploader_Succeeds()
    {
        var user = Principal(new Claim(ClaimTypes.NameIdentifier, UploaderUserId));
        var context = CreateContext(user);
        var handler = new UploadScopedResourceEnforcerHandler();

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_UnrestrictedPermission_SucceedsForAnyUploader()
    {
        var user = Principal(new Claim(
            BespokeAuthorizationPermissions.ClaimType,
            BespokeAuthorizationPermissions.Unrestricted));
        var context = CreateContext(user);
        var handler = new UploadScopedResourceEnforcerHandler();

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_ValidatorWithAllowValidators_SucceedsForOtherUploader()
    {
        var user = Principal(
            new Claim(BespokeAuthorizationPermissions.ClaimType, BespokeAuthorizationPermissions.UploadValidator),
            new Claim(ClaimTypes.NameIdentifier, "validator-user-id"));
        var context = CreateContext(user, true);
        var handler = new UploadScopedResourceEnforcerHandler();

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_ValidatorWithoutAllowValidators_DoesNotSucceedForOtherUploader()
    {
        var user = Principal(
            new Claim(BespokeAuthorizationPermissions.ClaimType, BespokeAuthorizationPermissions.UploadValidator),
            new Claim(ClaimTypes.NameIdentifier, "validator-user-id"));
        var context = CreateContext(user);
        var handler = new UploadScopedResourceEnforcerHandler();

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}

public class UploadDownloadResourceEnforcerHandlerTests
{
    private const string UploaderUserId = "uploader-user-id";

    private static AuthorizationHandlerContext CreateContext(
        ClaimsPrincipal user,
        UploadState state)
    {
        return new AuthorizationHandlerContext(
            [new UploadDownloadResourceRequirement()],
            user,
            new UploadDownloadResource(UploaderUserId, state));
    }

    private static ClaimsPrincipal Principal(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "test", "preferred_username", "role");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task HandleAsync_Uploader_DoesNotSucceedForNotValidatedUpload()
    {
        var user = Principal(new Claim(ClaimTypes.NameIdentifier, UploaderUserId));
        var context = CreateContext(user, UploadState.NotValidated);
        var handler = new UploadDownloadResourceEnforcerHandler();

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_Uploader_SucceedsForStoredUpload()
    {
        var user = Principal(new Claim(ClaimTypes.NameIdentifier, UploaderUserId));
        var context = CreateContext(user, UploadState.Stored);
        var handler = new UploadDownloadResourceEnforcerHandler();

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_Validator_SucceedsForNotValidatedUpload()
    {
        var user = Principal(new Claim(
            BespokeAuthorizationPermissions.ClaimType,
            BespokeAuthorizationPermissions.UploadValidator));
        var context = CreateContext(user, UploadState.NotValidated);
        var handler = new UploadDownloadResourceEnforcerHandler();

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }
}