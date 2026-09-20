using Microsoft.Extensions.Options;

namespace MediatrUnionPoc.Api.RequestTimeouts;

/// <summary>
/// Settings for the request timeouts registered by
/// <see cref="RequestTimeoutServiceCollectionExtensions.AddApiRequestTimeouts"/>: how long a request may run
/// before its <see cref="HttpContext.RequestAborted"/> token is cancelled and the caller is answered a
/// <c>504</c>. Bound from the <see cref="SectionName"/> configuration section and validated on start (by the
/// source-generated <see cref="RequestTimeoutOptionsValidator"/>). The defaults here equal the ones in
/// <c>appsettings.json</c>, so timeouts are on and bounded even with no configuration at all. Read through
/// <see cref="IOptions{TOptions}"/>: a change takes a restart, for the reason given on
/// <see cref="RateLimiting.RateLimitingOptions"/>. Values are <see cref="TimeSpan"/>s (<c>hh:mm:ss</c> or
/// <c>hh:mm:ss.fff</c>); sub-second values exist for tests and are never sensible in production.
/// </summary>
public sealed class RequestTimeoutOptions
{
    /// <summary>The configuration section the options bind from.</summary>
    public const string SectionName = "RequestTimeouts";

    /// <summary>The shortest timeout the options accept, one millisecond.</summary>
    public const string MinimumTimeout = "00:00:00.001";

    /// <summary>The longest timeout the options accept, ten minutes: a request that needs more is a job, not a request.</summary>
    public const string MaximumTimeout = "00:10:00";

    /// <summary>Gets or sets the timeout of every endpoint that does not name a policy or opt out. Defaults to 30 seconds.</summary>
    [TimeoutRange(MinimumTimeout, MaximumTimeout)]
    public TimeSpan Default { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the timeout of the impersonation-token endpoint. It does one signature and one audit
    /// append, so a healthy call takes milliseconds; a shorter budget than <see cref="Default"/> makes a stalled
    /// key provider or store fail fast instead of holding a connection of the most sensitive endpoint
    /// open for the whole default. Defaults to 10 seconds.
    /// </summary>
    [TimeoutRange(MinimumTimeout, MaximumTimeout)]
    public TimeSpan Impersonation { get; set; } = TimeSpan.FromSeconds(10);
}
