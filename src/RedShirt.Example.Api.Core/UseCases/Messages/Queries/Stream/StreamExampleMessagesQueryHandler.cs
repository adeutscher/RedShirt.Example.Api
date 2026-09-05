using RedShirt.Example.Api.ClientEvents.Domains.Example.Models;
using RedShirt.Example.Api.ClientEvents.Domains.Example.Services;
using RedShirt.Example.Api.Core.Cqrs;

namespace RedShirt.Example.Api.Core.UseCases.Messages.Queries.Stream;

public interface IStreamExampleMessagesQueryHandler : ICqrsHandler<StreamExampleMessagesQuery, IAsyncEnumerable<ExampleMessageModel>>;

internal sealed class StreamExampleMessagesQueryHandler(
    IExampleMessageReceiveService exampleMessageReceiveService,
    ICoreRequestValidator coreRequestValidator) : IStreamExampleMessagesQueryHandler
{
    public async Task<IAsyncEnumerable<ExampleMessageModel>> Handle(StreamExampleMessagesQuery query,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(query, cancellationToken);
        return exampleMessageReceiveService.ReceiveForUserAsync(query.UserId, cancellationToken);
    }
}
