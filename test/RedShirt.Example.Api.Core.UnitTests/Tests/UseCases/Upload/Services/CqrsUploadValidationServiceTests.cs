using Moq;
using RedShirt.Example.Api.Common.Exceptions.Responses;
using RedShirt.Example.Api.Core.UseCases.Upload.Commands.SubmitVerdict;
using RedShirt.Example.Api.Core.UseCases.Upload.Queries.GetContent;
using RedShirt.Example.Api.Core.UseCases.Upload.Queries.GetSummary;
using RedShirt.Example.Api.Core.UseCases.Upload.Services;
using RedShirt.Example.Api.Upload.Core.Models;
using RedShirt.Example.Api.Upload.Core.Models.Responses;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.UseCases.Upload.Services;

public class CqrsUploadValidationServiceTests
{
    private static readonly Guid UploadId = Guid.NewGuid();
    private readonly Mock<IGetUploadContentQueryHandler> _getContent = new();
    private readonly Mock<IGetUploadSummaryQueryHandler> _getSummary = new();
    private readonly Mock<ISubmitUploadVerdictCommandHandler> _submitVerdict = new();

    private static UploadSummaryModel CreateSummary(UploadState state)
    {
        return new UploadSummaryModel
        {
            Id = UploadId,
            DateCreatedUtc = DateTime.UtcNow,
            DateUpdatedUtc = DateTime.UtcNow,
            UploadedByUserId = "user-id",
            State = state,
            FileName = "document.txt",
            IsValidated = false,
            IsRejected = false
        };
    }

    private CqrsUploadValidationService CreateService()
    {
        return new CqrsUploadValidationService(_getSummary.Object, _getContent.Object, _submitVerdict.Object);
    }

    [Theory]
    [InlineData(UploadState.NotValidated, true)]
    [InlineData(UploadState.Uploading, false)]
    [InlineData(UploadState.Verified, false)]
    [InlineData(UploadState.Rejected, false)]
    [InlineData(UploadState.Deleted, false)]
    public async Task IsAwaitingValidationAsync_IsTrueOnlyForNotValidated(UploadState state, bool expected)
    {
        _getSummary.Setup(x => x.Handle(new GetUploadSummaryQuery(UploadId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSummary(state));

        Assert.Equal(expected,
            await CreateService().IsAwaitingValidationAsync(UploadId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IsAwaitingValidationAsync_WhenUploadIsGone_IsFalse()
    {
        _getSummary.Setup(x => x.Handle(It.IsAny<GetUploadSummaryQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ResourceNotFoundException());

        Assert.False(await CreateService().IsAwaitingValidationAsync(UploadId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task OpenContentAsync_UsesContentQuery()
    {
        await using var content = new MemoryStream();
        _getContent.Setup(x => x.Handle(new GetUploadContentQuery(UploadId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);

        Assert.Same(content, await CreateService().OpenContentAsync(UploadId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SubmitVerdictAsync_UsesVerdictCommand()
    {
        await CreateService().SubmitVerdictAsync(UploadId, true, TestContext.Current.CancellationToken);

        _submitVerdict.Verify(x => x.Handle(new SubmitUploadVerdictCommand(UploadId, true),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}