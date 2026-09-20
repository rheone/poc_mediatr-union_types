using System.ComponentModel.DataAnnotations;

namespace MediatrUnionPoc.Api.RateLimiting;

/// <summary>
/// The budget of one rate-limiting policy: at most <see cref="PermitLimit"/> requests per caller in
/// each fixed window of <see cref="WindowSeconds"/> seconds. Validated on start by the source-generated
/// <see cref="RateLimitPolicyOptionsValidator"/>, so a zero or absurd limit stops the host from starting
/// instead of silently blocking everyone or nobody.
/// </summary>
public sealed class RateLimitPolicyOptions
{
    /// <summary>The largest <see cref="PermitLimit"/>.</summary>
    public const int MaximumPermitLimit = 1_000_000;

    /// <summary>The largest <see cref="WindowSeconds"/>, one day.</summary>
    public const int MaximumWindowSeconds = 86_400;

    /// <summary>The largest <see cref="QueueLimit"/>.</summary>
    public const int MaximumQueueLimit = 1_000;

    /// <summary>Gets or sets how many requests one caller may make in one window; at least 1.</summary>
    [Range(1, MaximumPermitLimit)]
    public int PermitLimit { get; set; }

    /// <summary>Gets or sets the window length in seconds, from 1 to 86400.</summary>
    [Range(1, MaximumWindowSeconds)]
    public int WindowSeconds { get; set; }

    /// <summary>
    /// Gets or sets how many requests beyond the limit may wait for the next window instead of being
    /// rejected at once. Defaults to 0 (reject, never queue): a queued request holds a connection open, so
    /// a queue is a resource an attacker can fill.
    /// </summary>
    [Range(0, MaximumQueueLimit)]
    public int QueueLimit { get; set; }
}
