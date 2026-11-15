using Moq;
using RedShirt.Example.Api.BackgroundJobs.Core.Models;
using RedShirt.Example.Api.Common.Exceptions.Responses;
using RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Fail;
using RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Finish;
using RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Start;
using RedShirt.Example.Api.Core.UseCases.BackgroundJob.Services;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.UseCases.BackgroundJob.Services;

public class CqrsBackgroundJobStatusServiceTests
{
    private readonly Mock<IFailBackgroundJobCommandHandler> _failHandler = new();
    private readonly Mock<IFinishBackgroundJobCommandHandler> _finishHandler = new();
    private readonly Mock<IStartBackgroundJobCommandHandler> _startHandler = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static JobModel CreateJob(out Guid id)
    {
        var model = BackgroundJobTestData.CreateModel();
        id = model.Id;
        return CqrsBackgroundJobSource.ToJobModel(model);
    }

    private CqrsBackgroundJobStatusService CreateService()
    {
        return new CqrsBackgroundJobStatusService(_startHandler.Object, _finishHandler.Object, _failHandler.Object);
    }

    [Fact]
    public async Task ReportFailedAsync_SendsFailCommandWithLeasedAttempt()
    {
        var job = CreateJob(out var id);

        Assert.True(await CreateService().ReportFailedAsync(job, false, "bad", Token));

        _failHandler.Verify(x => x.Handle(new FailBackgroundJobCommand(id, job.Attempts, false, "bad"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReportFinishedAsync_SendsFinishCommandWithLeasedAttempt()
    {
        var job = CreateJob(out var id);

        Assert.True(await CreateService().ReportFinishedAsync(job, "done", Token));

        _finishHandler.Verify(x => x.Handle(new FinishBackgroundJobCommand(id, job.Attempts, "done"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReportStartedAsync_ForJobNotLeasedFromTheTable_Throws()
    {
        var job = new Mock<IJobModel>();
        job.SetupGet(x => x.MessageId).Returns(Guid.NewGuid().ToString());

        await Assert.ThrowsAsync<ArgumentException>(() => CreateService().ReportStartedAsync(job.Object, Token));
        _startHandler.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReportStartedAsync_SendsStartCommandWithLeasedAttempt()
    {
        var job = CreateJob(out var id);

        Assert.True(await CreateService().ReportStartedAsync(job, Token));

        _startHandler.Verify(x => x.Handle(new StartBackgroundJobCommand(id, job.Attempts),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reports_WhenFailingOtherwise_Throw()
    {
        var job = CreateJob(out _);
        _startHandler.Setup(x => x.Handle(It.IsAny<StartBackgroundJobCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().ReportStartedAsync(job, Token));
    }

    [Fact]
    public async Task Reports_WhenRefusedAsConflict_ReturnFalse()
    {
        var job = CreateJob(out _);
        _startHandler.Setup(x => x.Handle(It.IsAny<StartBackgroundJobCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("taken"));
        _finishHandler.Setup(x => x.Handle(It.IsAny<FinishBackgroundJobCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("taken"));
        _failHandler.Setup(x => x.Handle(It.IsAny<FailBackgroundJobCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("taken"));
        var service = CreateService();

        Assert.False(await service.ReportStartedAsync(job, Token));
        Assert.False(await service.ReportFinishedAsync(job, null, Token));
        Assert.False(await service.ReportFailedAsync(job, true, null, Token));
    }
}
