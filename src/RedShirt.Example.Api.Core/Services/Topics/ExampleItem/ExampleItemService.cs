using RedShirt.Example.Api.Core.Exceptions;
using RedShirt.Example.Api.Core.Exceptions.Responses;
using RedShirt.Example.Api.Core.Models;
using RedShirt.Example.Api.Core.Repositories;

namespace RedShirt.Example.Api.Core.Services.Topics.ExampleItem;

public interface IExampleItemService
{
    Task DeleteAsync(string name, CancellationToken cancellationToken = default);
    Task<ExampleItemModel> GetAsync(string name, CancellationToken cancellationToken = default);
    Task<ExampleItemListModel> GetListAsync(string? continuationToken, CancellationToken cancellationToken = default);

    Task<ExampleItemModel> PutAsync(ExampleItemModel model, string idempotencyKey,
        CancellationToken cancellationToken = default);
}

internal class ExampleItemService(IExampleItemRepository repository, ISubmissionIdempotencyService idempotencyService)
    : IExampleItemService
{
    public Task<ExampleItemModel> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BadRequestException("Name is required");
        }

        return repository.GetByName(name, cancellationToken);
    }

    public Task<ExampleItemListModel> GetListAsync(string? continuationToken,
        CancellationToken cancellationToken = default)
    {
        return repository.GetListAsync(continuationToken, cancellationToken);
    }

    public async Task<ExampleItemModel> PutAsync(ExampleItemModel model, string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        /* Pre-Execution Idempotency Check */

        if (await idempotencyService.GetRecordAsync<ExampleItemModel>(idempotencyKey, cancellationToken) is
            { } cachedResponse)
        {
            return cachedResponse;
        }

        var concurrentAttemptLock = await idempotencyService.GetLockAsync(idempotencyKey, cancellationToken);
        if (!concurrentAttemptLock.IsAcquired)
        {
            // Another instance of the handler is currently trying to process this same request
            throw new IdempotentConcurrencyException();
        }

        /* Execute */

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            throw new BadRequestException("Name is required");
        }

        await repository.Put(model, cancellationToken);

        /* Idempotency Cleanup */

        await idempotencyService.SetRecordAsync(idempotencyKey, model, cancellationToken);

        concurrentAttemptLock.Unlock();

        // Return
        return model;
    }

    public Task DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BadRequestException("Name is required");
        }

        return repository.DeleteByName(name, cancellationToken);
    }
}