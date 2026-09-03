using Moq;
using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.UseCases.Upload.Queries.GetDetails;
using RedShirt.Example.Api.Upload.Core.Models.Responses;
using RedShirt.Example.Api.Upload.Core.Services;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.UseCases.Upload.Queries.GetDetails;

public class GetUploadDetailsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsPublicDetailsModel()
    {
        var uploadId = Guid.NewGuid();
        var details = new UploadDetailsInternalModel
        {
            Id = uploadId,
            DateCreatedUtc = DateTime.UtcNow,
            UploadedByUserId = "user-id",
            UploadedByUsername = "user",
            UploaderIpAddress = "203.0.113.10",
            FileName = "file.txt",
            StorageObjectKey = "upload-id/user-id",
            VerifiedStorageObjectKey = "verified/upload-id/user-id",
            DateStoredUtc = DateTime.UtcNow
        };
        var uploadService = new Mock<IUploadService>();
        uploadService
            .Setup(x => x.GetDetailsAsync(uploadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);
        var validator = new Mock<ICoreRequestValidator>();
        validator
            .Setup(x => x.ValidateAsync(It.IsAny<GetUploadDetailsQuery>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var handler = new GetUploadDetailsQueryHandler(uploadService.Object, validator.Object);

        var result = await handler.Handle(
            new GetUploadDetailsQuery(uploadId),
            TestContext.Current.CancellationToken);

        Assert.Equal(uploadId, result.Id);
        Assert.Equal("file.txt", result.FileName);
        Assert.Equal(details.DateStoredUtc, result.DateStoredUtc);
    }
}