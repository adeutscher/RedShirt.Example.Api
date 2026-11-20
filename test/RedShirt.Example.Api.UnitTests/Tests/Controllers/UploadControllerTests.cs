using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RedShirt.Example.Api.Authorization.Constants;
using RedShirt.Example.Api.Authorization.ResourceScoping.Upload;
using RedShirt.Example.Api.Common.Exceptions.Responses;
using RedShirt.Example.Api.Controllers;
using RedShirt.Example.Api.Core.UseCases.Upload.Commands.Delete;
using RedShirt.Example.Api.Core.UseCases.Upload.Queries.GetSummary;
using System.Security.Claims;

namespace RedShirt.Example.Api.UnitTests.Tests.Controllers;

public class UploadControllerTests
{
    private static readonly Guid UploadId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static UploadController Controller(params string[] permissions)
    {
        var claims = new List<Claim> {new("sub", "user-1")};
        claims.AddRange(permissions.Select(permission =>
            new Claim(BespokeAuthorizationPermissions.ClaimType, permission)));

        return new UploadController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
                }
            }
        };
    }

    [Fact]
    public async Task Delete_Purge_WithoutPurgePermission_ForbidsBeforeLookingUpUpload()
    {
        var summaryHandler = new Mock<IGetUploadSummaryQueryHandler>();
        summaryHandler
            .Setup(handler => handler.Handle(It.IsAny<GetUploadSummaryQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ResourceNotFoundException());
        var enforcer = new Mock<IUploadScopedResourceEnforcer>();
        var deleteHandler = new Mock<IDeleteUploadCommandHandler>();

        var result = await Controller(BespokeAuthorizationPermissions.UploadWrite).Delete(
            UploadId, true, summaryHandler.Object, enforcer.Object, deleteHandler.Object,
            TestContext.Current.CancellationToken);

        Assert.IsType<ForbidResult>(result);
        summaryHandler.Verify(
            handler => handler.Handle(It.IsAny<GetUploadSummaryQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
        deleteHandler.Verify(
            handler => handler.Handle(It.IsAny<DeleteUploadCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Delete_Purge_WithPurgePermission_MissingUpload_IsNotFound()
    {
        var summaryHandler = new Mock<IGetUploadSummaryQueryHandler>();
        summaryHandler
            .Setup(handler => handler.Handle(It.IsAny<GetUploadSummaryQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ResourceNotFoundException());
        var enforcer = new Mock<IUploadScopedResourceEnforcer>();
        var deleteHandler = new Mock<IDeleteUploadCommandHandler>();

        await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            Controller(BespokeAuthorizationPermissions.UploadPurge).Delete(
                UploadId, true, summaryHandler.Object, enforcer.Object, deleteHandler.Object,
                TestContext.Current.CancellationToken));

        deleteHandler.Verify(
            handler => handler.Handle(It.IsAny<DeleteUploadCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
