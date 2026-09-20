namespace MediatrUnionPoc.Application.Common.Auditing;

/// <summary>
/// What <see cref="Behaviors.AuditBehavior{TRequest,TResponse}"/> does when the audit event cannot be
/// written. The choice is per request, made by the request itself
/// (<see cref="Abstractions.IAuditableRequest{TResponse}.AuditFailurePolicy"/>), because it depends on
/// whether the action can still be undone.
/// </summary>
public enum AuditFailurePolicy
{
    /// <summary>
    /// The request fails: an <see cref="AuditWriteFailedException"/> is thrown after the failure is
    /// logged at Error, and the response the handler produced (a credential, say) is never delivered.
    /// For actions that must not happen unrecorded.
    /// </summary>
    FailClosed = 0,

    /// <summary>
    /// The failure is logged at Error and the response proceeds. For actions that have already taken
    /// effect (a committed mutation) where failing the request would misreport what happened.
    /// </summary>
    BestEffort = 1,
}
