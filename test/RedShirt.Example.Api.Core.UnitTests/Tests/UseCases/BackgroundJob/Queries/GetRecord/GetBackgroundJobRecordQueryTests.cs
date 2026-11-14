using Moq;
using RedShirt.Example.Api.BackgroundJobs.Core.Services;
using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.UseCases.BackgroundJob.Queries.GetRecord;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.UseCases.BackgroundJob.Queries.GetRecord;

public class GetBackgroundJobRecordQueryTests
{
    [Fact]
    public async Task Handle_ValidatesQuery_AndGetsRecord()
    {
        var model = BackgroundJobTestData.CreateModel();
        var service = new Mock<IBackgroundJobService>();
        service.Setup(x => x.GetAsync(model.Id, It.IsAny<CancellationToken>())).ReturnsAsync(model);
        var validator = new Mock<ICoreRequestValidator>();
        var query = new GetBackgroundJobRecordQuery(model.Id);

        var result = await new GetBackgroundJobRecordQueryHandler(service.Object, validator.Object)
            .Handle(query, TestContext.Current.CancellationToken);

        Assert.Same(model, result);
        validator.Verify(x => x.ValidateAsync(query, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Validator_Fails_ForEmptyId()
    {
        var validation = await new GetBackgroundJobRecordQueryValidator().ValidateAsync(
            new GetBackgroundJobRecordQuery(Guid.Empty), TestContext.Current.CancellationToken);

        Assert.False(validation.IsValid);
    }
}
