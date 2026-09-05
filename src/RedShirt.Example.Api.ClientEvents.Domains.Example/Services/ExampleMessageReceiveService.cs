using RedShirt.Example.Api.ClientEvents.Domains.Example.Models;
using RedShirt.Example.Api.ClientEvents.Domains.Example.Utilities;
using RedShirt.Example.Api.ClientEvents.Library.Core.Services;
using System.Runtime.CompilerServices;

namespace RedShirt.Example.Api.ClientEvents.Domains.Example.Services;

public interface IExampleMessageReceiveService
{
    IAsyncEnumerable<ExampleMessageModel> ReceiveForUserAsync(string userId,
        CancellationToken cancellationToken = default);
}

internal sealed class ExampleMessageReceiveService(IApiClientEventReceiver<ExampleMessageModel> eventReceiver)
    : IExampleMessageReceiveService
{
    public async IAsyncEnumerable<ExampleMessageModel> ReceiveForUserAsync(string userId,
        [EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        var topic = ExampleMessageTopicNames.ForUser(userId);
        await foreach (var received in eventReceiver.ReceiveAsync([topic], cancellationToken))
        {
            yield return received.Payload;
        }
    }
}