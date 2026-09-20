using System.Diagnostics;
using MediatrUnionPoc.Api.Http;
using MediatrUnionPoc.Application.Common.Auditing;

namespace MediatrUnionPoc.Api.Audit;

/// <summary>
/// The Api's <see cref="IAuditRequestContext"/>: the current request's trace id
/// (<see cref="HttpContextTraceExtensions"/> semantics, so it equals the response's <c>X-Trace-Id</c>)
/// and the connection's remote address. Behind a reverse proxy the address is the proxy's unless
/// forwarded headers are enabled for the host.
/// </summary>
/// <param name="accessor">Gives the request the audited command runs in.</param>
/// <exception cref="ArgumentNullException"><paramref name="accessor"/> is <see langword="null"/>.</exception>
public sealed class HttpAuditRequestContext(IHttpContextAccessor accessor) : IAuditRequestContext
{
    private readonly IHttpContextAccessor _accessor =
        accessor ?? throw new ArgumentNullException(nameof(accessor));

    /// <inheritdoc/>
    public string? TraceId =>
        _accessor.HttpContext?.TraceId
        ?? (Activity.Current?.TraceId is { } id && id != default ? id.ToHexString() : null);

    /// <inheritdoc/>
    public string? SourceIp => _accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
