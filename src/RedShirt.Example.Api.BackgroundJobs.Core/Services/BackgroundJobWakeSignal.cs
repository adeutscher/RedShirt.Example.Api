namespace RedShirt.Example.Api.BackgroundJobs.Core.Services;

/// <summary>
///     Lets a submission in this process cut the runner's idle wait short, so a job submitted here starts promptly
///     instead of after the next poll. A job submitted elsewhere still waits for the next poll.
/// </summary>
public interface IBackgroundJobWakeSignal
{
    /// <summary>
    ///     Waits for <paramref name="delay" /> or a wake, whichever comes first.
    /// </summary>
    /// <returns>True when woken.</returns>
    Task<bool> WaitAsync(TimeSpan delay, CancellationToken cancellationToken = default);

    void Wake();
}

internal sealed class BackgroundJobWakeSignal : IBackgroundJobWakeSignal, IDisposable
{
    private readonly SemaphoreSlim _signal = new(0, 1);

    public Task<bool> WaitAsync(TimeSpan delay, CancellationToken cancellationToken = default)
    {
        return _signal.WaitAsync(delay, cancellationToken);
    }

    public void Wake()
    {
        try
        {
            _signal.Release();
        }
        catch (SemaphoreFullException)
        {
            // Already woken.
        }
    }

    public void Dispose()
    {
        _signal.Dispose();
    }
}