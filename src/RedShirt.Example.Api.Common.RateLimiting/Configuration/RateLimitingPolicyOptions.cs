namespace RedShirt.Example.Api.Common.RateLimiting.Configuration;

internal sealed class RateLimitingPolicyOptions
{
    /// <summary>
    ///     Set an override name to the configuration.
    ///     In the absence of this option, the default behaviour of policy loading is to treat the policy as named in its
    ///     configuration path as the policy name.
    ///     Setting a name is encouraged for specificity, as otherwise you are at the mercy of the environment variable parsing
    ///     implementation.
    ///     If you are using Redis, then the combination of Name and RedisKeyPrefix should be unique across all defined
    ///     policies.
    /// </summary>
    public required string? Name { get; init; }

    /// <summary>
    ///     In the event of Redis being enabled, the values that are set will be prefixed with this value.
    ///     If no value is provided, then the prefix will just be "redis".
    ///     If you are using Redis, then the combination of Name and RedisKeyPrefix should be unique across all defined
    ///     policies.
    /// </summary>
    public required string? RedisKeyPrefix { get; init; }

    /// <summary>
    ///     Maximum number of calls that a client is allowed to make within the window.
    /// </summary>
    public required int WindowPermitLimit { get; init; }

    /// <summary>
    ///     Defines the window size in minutes.
    /// </summary>
    public required int LimitWindowMinutes { get; init; }

    /// <summary>
    ///     Configured window expressed as a TimeSpan.
    /// </summary>
    public TimeSpan Window => TimeSpan.FromMinutes(LimitWindowMinutes);

    /// <summary>
    ///     If Redis enabled but unavailable, then allow the request (false) or reject with 429 (true).
    /// </summary>
    public required bool FailClosed { get; init; }
}