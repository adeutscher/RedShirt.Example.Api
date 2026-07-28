using RedShirt.Example.Api.Core.Cqrs;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.Cqrs.Fixtures;

public record SampleCommand;

public record SampleResult(string Name);

public interface ISampleCommandHandler : ICqrsHandler<SampleCommand, SampleResult>;

internal class SampleCommandHandler : ISampleCommandHandler
{
    public Task<SampleResult> Handle(SampleCommand request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new SampleResult("foo"));
    }
}

public interface ISampleVoidCommandHandler : ICqrsHandler<SampleCommand>;

internal class SampleVoidCommandHandler : ISampleVoidCommandHandler
{
    public Task Handle(SampleCommand request, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

internal class DirectCqrsHandler : ICqrsHandler<SampleCommand>
{
    public Task Handle(SampleCommand request, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

public abstract class AbstractSampleCommandHandler : ISampleCommandHandler
{
    public abstract Task<SampleResult> Handle(SampleCommand request,
        CancellationToken cancellationToken = default);
}
