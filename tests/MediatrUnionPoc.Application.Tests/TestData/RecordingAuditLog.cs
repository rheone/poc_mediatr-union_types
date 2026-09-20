using MediatrUnionPoc.Application.Common.Auditing;

namespace MediatrUnionPoc.Application.Tests.TestData;

/// <summary>
/// An <see cref="IAuditLog"/> that keeps every event it is given, or, when told to, fails the way a
/// broken store does. Also remembers the cancellation token each write was given.
/// </summary>
public sealed class RecordingAuditLog : IAuditLog
{
    private readonly List<AuditEvent> _events = [];

    /// <summary>Gets or sets the exception every write throws; <see langword="null"/> (the default) means writes succeed.</summary>
    public Exception? FailWith { get; set; }

    /// <summary>Gets the events recorded so far, in write order.</summary>
    public IReadOnlyList<AuditEvent> Events => _events;

    /// <summary>Gets the token the most recent write was given.</summary>
    public CancellationToken LastToken { get; private set; }

    /// <inheritdoc/>
    public Task RecordAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        LastToken = cancellationToken;

        if (FailWith is { } failure)
        {
            return Task.FromException(failure);
        }

        _events.Add(auditEvent);
        return Task.CompletedTask;
    }
}
