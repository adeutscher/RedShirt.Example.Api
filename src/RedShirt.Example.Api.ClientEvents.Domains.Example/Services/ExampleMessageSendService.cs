using RedShirt.Example.Api.ClientEvents.Domains.Example.Models;
using RedShirt.Example.Api.ClientEvents.Domains.Example.Utilities;
using RedShirt.Example.Api.ClientEvents.Library.Core.Models;
using RedShirt.Example.Api.ClientEvents.Library.Core.Services;

namespace RedShirt.Example.Api.ClientEvents.Domains.Example.Services;

public interface IExampleMessageSendService
{
    Task SendToUserAsync(string userId, ExampleMessageModel message, CancellationToken cancellationToken = default);
}

internal sealed class ExampleMessageSendService(IApiClientEventSender<ExampleMessageModel> eventSender)
    : IExampleMessageSendService
{
    public Task SendToUserAsync(string userId, ExampleMessageModel message,
        CancellationToken cancellationToken = default)
    {
        return eventSender.SendAsync(new ApiClientEventSendRequest<ExampleMessageModel>
        {
            Payload = message,
            Topic = ExampleMessageTopicNames.ForUser(userId)
        }, cancellationToken);
    }
}