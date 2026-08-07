using RedShirt.Example.Api.Core.Exceptions;

namespace RedShirt.Example.Api.Core.Services;

public interface ICacheBasedIdempotencyWrapperService
{
    /// <summary>
    ///     Wrapper service around cache-based idempotent operations.
    ///     Repeated attempts will be served out of a cache.
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

public class CacheBasedIdempotencyWrapperService(ICacheBasedIdempotencyService idempotencyService)
    : ICacheBasedIdempotencyWrapperService
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

        var concurrentAttemptLock = await idempotencyService.GetLockAsync(idempotencyKey, cancellationToken);
        if (!concurrentAttemptLock.IsAcquired)
        {
            // Another instance of the handler is currently trying to process this same request
            throw new IdempotentConcurrencyException();
        }

        try
        {
            if (await idempotencyService.GetRecordAsync<T>(idempotencyKey, cancellationToken) is
                { } cachedResponse)
            {
                return cachedResponse;
            }

            var model = await callback();

            /* Idempotency Cleanup */

            await idempotencyService.SetRecordAsync(idempotencyKey, model, cancellationToken);

            // Return
            return model;
        }
        finally
        {
            await concurrentAttemptLock.UnlockAsync(cancellationToken);
        }
    }
}