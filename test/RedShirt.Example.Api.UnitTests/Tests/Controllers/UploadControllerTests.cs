using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RedShirt.Example.Api.Authorization.Constants;
using RedShirt.Example.Api.Authorization.ResourceScoping.Upload;
using RedShirt.Example.Api.Common.Exceptions.Responses;
using RedShirt.Example.Api.Controllers;
using RedShirt.Example.Api.Core.UseCases.Upload.Commands.Delete;
using RedShirt.Example.Api.Core.UseCases.Upload.Queries.GetSummary;
using RedShirt.Example.Api.Extensions;
using RedShirt.Example.Api.Upload.Core.Models;
using RedShirt.Example.Api.Upload.Core.Models.Responses;
using System.Security.Claims;

namespace RedShirt.Example.Api.UnitTests.Tests.Controllers;

/// <summary>
///     Uses the real claims transformation and upload enforcer, so each case runs against the role map and the
///     authorization policies as configured.
/// </summary>
public sealed class UploadControllerTests : IDisposable
{
    private const string CallerUserId = "user-1";
    private const string OtherUserId = "user-2";
    private static readonly Guid UploadId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly Mock<IDeleteUploadCommandHandler> _deleteHandler = new();
    private readonly ServiceProvider _provider;
    private readonly Mock<IGetUploadSummaryQueryHandler> _summaryHandler = new();

    public UploadControllerTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApiAuthorizationPolicies();
        _provider = services.BuildServiceProvider();
    }

    private IUploadScopedResourceEnforcer Enforcer => _provider.GetRequiredService<IUploadScopedResourceEnforcer>();

    public void Dispose()
    {
        _provider.Dispose();
    }

    private async Task<UploadController> ControllerForRoles(params string[] roles)
    {
        var claims = new List<Claim> {new("sub", CallerUserId)};
        claims.AddRange(roles.Select(role => new Claim("role", role)));
        var identity = new ClaimsIdentity(claims, "test", "preferred_username", "role");
        var user = await _provider.GetRequiredService<IClaimsTransformation>()
            .TransformAsync(new ClaimsPrincipal(identity));

        return new UploadController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext {User = user}
            }
        };
    }

    private void UploadOwnedBy(string uploadedByUserId)
    {
        _summaryHandler
            .Setup(handler => handler.Handle(It.IsAny<GetUploadSummaryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadSummaryModel
            {
                Id = UploadId,
                DateCreatedUtc = DateTime.UtcNow,
                DateUpdatedUtc = DateTime.UtcNow,
                UploadedByUserId = uploadedByUserId,
                State = UploadState.Stored,
                FileName = "file.txt",
                IsValidated = true,
                IsRejected = false
            });
    }

    private Task<IActionResult> Purge(UploadController controller)
    {
        return controller.Delete(UploadId, true, _summaryHandler.Object, Enforcer, _deleteHandler.Object,
            TestContext.Current.CancellationToken);
    }

    private void VerifyPurged(Times times)
    {
        _deleteHandler.Verify(
            handler => handler.Handle(It.Is<DeleteUploadCommand>(command => command.Id == UploadId && command.Purge),
                It.IsAny<CancellationToken>()),
            times);
    }

    [Theory]
    [InlineData(BespokeAuthorizationRoles.Analyst)]
    [InlineData(BespokeAuthorizationRoles.Billing)]
    [InlineData(BespokeAuthorizationRoles.UploadValidator)]
    [InlineData("unmapped-role")]
    public async Task Delete_Purge_RoleWithoutPurgePermission_ForbidsEvenForOwnUpload(string role)
    {
        UploadOwnedBy(CallerUserId);

        var result = await Purge(await ControllerForRoles(role));

        Assert.IsType<ForbidResult>(result);
        _summaryHandler.Verify(
            handler => handler.Handle(It.IsAny<GetUploadSummaryQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
        VerifyPurged(Times.Never());
    }

    [Fact]
    public async Task Delete_Purge_Developer_OwnUpload_Purges()
    {
        UploadOwnedBy(CallerUserId);

        var result = await Purge(await ControllerForRoles(BespokeAuthorizationRoles.Developer));

        Assert.IsType<NoContentResult>(result);
        VerifyPurged(Times.Once());
    }

    [Fact]
    public async Task Delete_Purge_Developer_OtherUsersUpload_IsNotFound()
    {
        UploadOwnedBy(OtherUserId);
        var controller = await ControllerForRoles(BespokeAuthorizationRoles.Developer);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => Purge(controller));

        VerifyPurged(Times.Never());
    }

    [Fact]
    public async Task Delete_Purge_Admin_OtherUsersUpload_Purges()
    {
        UploadOwnedBy(OtherUserId);

        var result = await Purge(await ControllerForRoles(BespokeAuthorizationRoles.Admin));

        Assert.IsType<NoContentResult>(result);
        VerifyPurged(Times.Once());
    }

    [Fact]
    public async Task Delete_Purge_MissingUpload_IsNotFound()
    {
        _summaryHandler
            .Setup(handler => handler.Handle(It.IsAny<GetUploadSummaryQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ResourceNotFoundException());
        var controller = await ControllerForRoles(BespokeAuthorizationRoles.Admin);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => Purge(controller));

        VerifyPurged(Times.Never());
    }
}
