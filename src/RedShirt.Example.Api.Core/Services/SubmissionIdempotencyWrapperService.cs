using RedShirt.Example.Api.Core.Exceptions;

namespace RedShirt.Example.Api.Core.Services;

public interface ISubmissionIdempotencyWrapperService
{
    /// <summary>
    ///     Run idempotent operation
    /// </summary>
    /// <param name="idempotencyKey"></param>
    /// <param name="callback"></param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="RedShirt.Example.Api.Core.Exceptions.IdempotentConcurrencyException">
    ///     Thrown if another instance of an
    ///     idempotent operation is currently running.
    /// </exception>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    Task<T> RunIdempotentlyAsync<T>(string idempotencyKey, Func<Task<T>> callback,
        CancellationToken cancellationToken = default) where T : class;
}

public class SubmissionIdempotencyWrapperService(ISubmissionIdempotencyService idempotencyService)
    : ISubmissionIdempotencyWrapperService
{
    /// <summary>
    ///     This implementation captures the common flow of checking operations for idempotence
    /// </summary>
    /// <param name="idempotencyKey"></param>
    /// <param name="callback"></param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    /// <exception cref="IdempotentConcurrencyException"></exception>
    public async Task<T> RunIdempotentlyAsync<T>(string idempotencyKey, Func<Task<T>> callback,
        CancellationToken cancellationToken = default) where T : class
    {
        /* Pre-Execution Idempotency Check */

        if (await idempotencyService.GetRecordAsync<T>(idempotencyKey, cancellationToken) is
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

        var model = await callback();

        /* Idempotency Cleanup */

        await idempotencyService.SetRecordAsync(idempotencyKey, model, cancellationToken);

        concurrentAttemptLock.Unlock();

        // Return
        return model;
    }
}