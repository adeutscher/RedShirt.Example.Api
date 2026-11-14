using Moq;
using RedShirt.Example.Api.BackgroundJobs.Core.Models;
using RedShirt.Example.Api.BackgroundJobs.Core.Services;
using RedShirt.Example.Api.BackgroundJobs.Submission.Models;
using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.UseCases.BackgroundJob.Queries.SearchRecords;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.UseCases.BackgroundJob.Queries.SearchRecords;

public class SearchBackgroundJobRecordsQueryTests
{
    [Fact]
    public async Task Handle_ValidatesQuery_AndMapsToServiceRequest()
    {
        var response = new BackgroundJobSearchResponse {Records = [BackgroundJobTestData.CreateModel()]};
        var createdBefore = DateTime.UtcNow;
        BackgroundJobServiceSearchRequest? captured = null;
        var service = new Mock<IBackgroundJobService>();
        service.Setup(x => x.SearchAsync(It.IsAny<BackgroundJobServiceSearchRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<BackgroundJobServiceSearchRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(response);
        var validator = new Mock<ICoreRequestValidator>();
        var query = new SearchBackgroundJobRecordsQuery(10, BackgroundJobType.ValidateUpload,
            BackgroundJobState.Failed, createdBefore);

        var result = await new SearchBackgroundJobRecordsQueryHandler(service.Object, validator.Object)
            .Handle(query, TestContext.Current.CancellationToken);

        Assert.Same(response, result);
        validator.Verify(x => x.ValidateAsync(query, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(10, captured!.PageSize);
        Assert.Equal(BackgroundJobType.ValidateUpload, captured.Type);
        Assert.Equal(BackgroundJobState.Failed, captured.State);
        Assert.Equal(createdBefore, captured.CreatedBeforeUtc);
    }

    [Theory]
    [InlineData(0, null, null, true)]
    [InlineData(-1, null, null, false)]
    [InlineData(0, 99, null, false)]
    [InlineData(0, null, 99, false)]
    public async Task Validator_ChecksFields(int pageSize, int? type, int? state, bool expected)
    {
        var validation = await new SearchBackgroundJobRecordsQueryValidator().ValidateAsync(
            new SearchBackgroundJobRecordsQuery(pageSize, (BackgroundJobType?) type, (BackgroundJobState?) state,
                null),
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, validation.IsValid);
    }
}
