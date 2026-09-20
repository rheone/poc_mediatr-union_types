using System.Diagnostics;

namespace MediatrUnionPoc.Application.Common.Auditing;

/// <summary>
/// The ambient facts about the request being audited that the Application layer cannot read for
/// itself because it has no <c>HttpContext</c>. The host implements it over the current request.
/// </summary>
public interface IAuditRequestContext
{
    /// <summary>Gets the correlation id shared with the operational log and the response's <c>X-Trace-Id</c>, or <see langword="null"/> when unknown.</summary>
    string? TraceId { get; }

    /// <summary>Gets the caller's network address as the server sees it, or <see langword="null"/> when unknown.</summary>
    string? SourceIp { get; }
}

/// <summary>
/// The context used when the host registers none: the trace id of the ambient <see cref="Activity"/>
/// and no source address. Keeps the pipeline resolvable in hosts and tests without a web request.
/// </summary>
internal sealed class AmbientAuditRequestContext : IAuditRequestContext
{
    /// <inheritdoc/>
    public string? TraceId =>
        Activity.Current?.TraceId is { } id && id != default ? id.ToHexString() : null;

    /// <inheritdoc/>
    public string? SourceIp => null;
}
