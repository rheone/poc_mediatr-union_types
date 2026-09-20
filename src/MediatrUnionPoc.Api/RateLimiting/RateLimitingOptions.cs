using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace MediatrUnionPoc.Api.RateLimiting;

/// <summary>
/// Settings for the rate limiter registered by
/// <see cref="RateLimitingServiceCollectionExtensions.AddApiRateLimiting"/>: one budget per policy
/// (<see cref="RateLimitPolicyNames"/>). Bound from the <see cref="SectionName"/> configuration section
/// and validated on start (by the source-generated <see cref="RateLimitingOptionsValidator"/>). The
/// defaults here equal the ones in <c>appsettings.json</c>, so the limiter is on and bounded even with no
/// configuration at all. Read through <see cref="IOptions{TOptions}"/>: a change takes a restart.
/// Live reload through <see cref="IOptionsMonitor{TOptions}"/> was tried and rejected: once an invalid value
/// is written to a watched settings file the monitor's current value throws, so every caller arriving after
/// would be answered 500 (the reload itself throws too, as it does for every validated options class in this
/// host). Read once, the limiter keeps the limits it started with. For a security control that is a better
/// failure than a broken limiter, and the next start refuses the bad value.
/// </summary>
public sealed class RateLimitingOptions
{
    /// <summary>The configuration section the options bind from.</summary>
    public const string SectionName = "RateLimiting";

    /// <summary>Gets or sets the budget for reads and for any action that names no policy. Defaults to 120 requests per 60 seconds.</summary>
    [Required]
    [ValidateObjectMembers(typeof(RateLimitPolicyOptionsValidator))]
    public RateLimitPolicyOptions Reads { get; set; } =
        new() { PermitLimit = 120, WindowSeconds = 60 };

    /// <summary>Gets or sets the budget for mutating product requests. Defaults to 30 requests per 60 seconds.</summary>
    [Required]
    [ValidateObjectMembers(typeof(RateLimitPolicyOptionsValidator))]
    public RateLimitPolicyOptions Writes { get; set; } =
        new() { PermitLimit = 30, WindowSeconds = 60 };

    /// <summary>Gets or sets the budget for minting impersonation tokens, per real caller. Defaults to 5 requests per 60 seconds.</summary>
    [Required]
    [ValidateObjectMembers(typeof(RateLimitPolicyOptionsValidator))]
    public RateLimitPolicyOptions Impersonation { get; set; } =
        new() { PermitLimit = 5, WindowSeconds = 60 };
}
