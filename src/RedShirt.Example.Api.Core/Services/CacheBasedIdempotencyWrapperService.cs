using RedShirt.Example.Api.Common.Exceptions.Responses;

namespace RedShirt.Example.Api.Core.Services;

public interface ICacheBasedIdempotencyWrapperService
{
    /// <summary>
    ///     Wrapper service around cache-based idempotent operations.
    ///     Repeated attempts will be served out of a cache.
    /// </summary>
    /// <param name="scope">Operation, caller and client key. All three are part of the cache identity.</param>
    /// <param name="callback"></param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="ConflictException">
    ///     Thrown if another instance of an
    ///     idempotent operation is currently running.
    /// </exception>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    Task<T> RunIdempotentlyAsync<T>(IdempotencyScope scope, Func<Task<T>> callback,
        CancellationToken cancellationToken = default) where T : class;
}

public class CacheBasedIdempotencyWrapperService(ICacheBasedIdempotencyService idempotencyService)
    : ICacheBasedIdempotencyWrapperService
{
    /// <summary>
    ///     This implementation captures the common flow of checking operations for idempotence
    /// </summary>
    /// <param name="scope">Operation, caller and client key. All three are part of the cache identity.</param>
    /// <param name="callback"></param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    /// <exception cref="ConflictException"></exception>
    public async Task<T> RunIdempotentlyAsync<T>(IdempotencyScope scope, Func<Task<T>> callback,
        CancellationToken cancellationToken = default) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope.Operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope.CallerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope.Key);

        /* Pre-Execution Idempotency Check */

        var concurrentAttemptLock = await idempotencyService.GetLockAsync(scope, cancellationToken);
        if (!concurrentAttemptLock.IsAcquired)
        {
            // Another instance of the handler is currently trying to process this same request
            throw new ConflictException(
                "An idempotent operation was requested while a previous instance of the same operation was in motion.");
        }

        try
        {
            if (await idempotencyService.GetRecordAsync<T>(scope, cancellationToken) is
                { } cachedResponse)
            {
                return cachedResponse;
            }

            var model = await callback();

            /* Idempotency Cleanup */

            await idempotencyService.SetRecordAsync(scope, model, cancellationToken);

            // Return
            return model;
        }
        finally
        {
            await concurrentAttemptLock.UnlockAsync(cancellationToken);
        }
    }
}