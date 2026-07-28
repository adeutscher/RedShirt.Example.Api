namespace RedShirt.Example.Api.Core.Cqrs;

public interface ICqrsHandler<in TIn>
{
    Task Handle(TIn request, CancellationToken cancellationToken = default);
}

public interface ICqrsHandler<in TIn, TOut>
{
    Task<TOut> Handle(TIn request, CancellationToken cancellationToken = default);
}
