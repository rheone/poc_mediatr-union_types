namespace MediatrUnionPoc.Application.Common.Auditing;

/// <summary>
/// The append-only record of security-relevant actions, kept apart from diagnostic logging: it is
/// never sampled, never level-filtered and never shares a sink with the operational log. An
/// abstraction so the file writer the host ships today can be replaced by a database table or a
/// message queue without changing any caller.
/// </summary>
/// <remarks>
/// Implementations must not swallow a write failure: they throw, and the caller applies its
/// <see cref="AuditFailurePolicy"/>. Auditing cannot be switched off; a host that registers no
/// implementation fails when the first auditable request resolves its pipeline.
/// </remarks>
public interface IAuditLog
{
    /// <summary>Durably appends <paramref name="auditEvent"/>.</summary>
    /// <param name="auditEvent">The event to record.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the event is stored.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="auditEvent"/> is <see langword="null"/>.</exception>
    /// <exception cref="IOException">The event could not be stored (the file implementation; other stores throw their own exception types).</exception>
    Task RecordAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
}
