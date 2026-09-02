using Moq;
using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.UseCases.Upload.Queries.GetInternalDetails;
using RedShirt.Example.Api.Upload.Core.Models.Responses;
using RedShirt.Example.Api.Upload.Core.Services;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.UseCases.Upload.Queries.GetInternalDetails;

public class GetUploadInternalDetailsQueryHandlerTests
{
    [Fact]
    public async Task Handle_MapsDetailsToInternalModel()
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
            StorageObjectKey = "upload-id/user-id"
        };
        var uploadService = new Mock<IUploadService>();
        uploadService
            .Setup(x => x.GetDetailsAsync(uploadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);
        var validator = new Mock<ICoreRequestValidator>();
        validator
            .Setup(x => x.ValidateAsync(It.IsAny<GetUploadInternalDetailsQuery>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var handler = new GetUploadInternalDetailsQueryHandler(uploadService.Object, validator.Object);

        var result = await handler.Handle(
            new GetUploadInternalDetailsQuery(uploadId),
            TestContext.Current.CancellationToken);

        Assert.Equal("203.0.113.10", result.UploaderIpAddress);
        Assert.Equal("upload-id/user-id", result.StorageObjectKey);
    }
}
