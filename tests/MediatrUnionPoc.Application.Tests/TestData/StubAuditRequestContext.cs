using MediatrUnionPoc.Application.Common.Auditing;

namespace MediatrUnionPoc.Application.Tests.TestData;

/// <summary>An <see cref="IAuditRequestContext"/> with fixed values.</summary>
/// <param name="traceId">The trace id reported.</param>
/// <param name="sourceIp">The source address reported.</param>
public sealed class StubAuditRequestContext(
    string? traceId = "trace-1",
    string? sourceIp = "203.0.113.7"
) : IAuditRequestContext
{
    /// <inheritdoc/>
    public string? TraceId => traceId;

    /// <inheritdoc/>
    public string? SourceIp => sourceIp;
}
