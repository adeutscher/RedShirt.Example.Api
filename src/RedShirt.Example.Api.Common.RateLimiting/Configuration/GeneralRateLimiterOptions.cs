namespace RedShirt.Example.Api.Common.RateLimiting.Configuration;

internal sealed class GeneralRateLimiterOptions
{
    /// <summary>
    ///     Turn off rate-limiting entirely.
    ///     If set to true, relevant middleware will not be added to the application and directly rate-limiting-related
    ///     services will not be added to dependency injection.
    /// </summary>
    public required bool DisableRateLimiting { get; init; }

    /// <summary>
    ///     When true and Redis is configured, use shared Redis sliding window for rate limiting.
    ///     When false (or Redis connection missing), fall back to in-memory sliding window.
    /// </summary>
    public required bool UseRedis { get; init; }

    /// <summary>
    ///     If set, the API will require rate limiting under this policy.
    /// </summary>
    public required string? DefaultPolicyName { get; init; }
}