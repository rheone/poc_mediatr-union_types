namespace MediatrUnionPoc.Application.Common.Auditing;

/// <summary>
/// Thrown by <see cref="Behaviors.AuditBehavior{TRequest,TResponse}"/> for a
/// <see cref="AuditFailurePolicy.FailClosed"/> request whose audit event could not be written. It is
/// an infrastructure fault, not an expected outcome, so it is an exception: the host's exception
/// handler answers it with a 500 and the response the handler produced is discarded.
/// </summary>
/// <param name="message">The message.</param>
/// <param name="innerException">The write failure.</param>
public sealed class AuditWriteFailedException(string? message, Exception? innerException)
    : Exception(message, innerException)
{
    /// <summary>Creates the exception for an action whose event could not be written.</summary>
    /// <param name="action">The audited action.</param>
    /// <param name="innerException">The write failure.</param>
    /// <returns>The exception.</returns>
    public static AuditWriteFailedException ForAction(string action, Exception innerException) =>
        new($"The audit event for '{action}' could not be recorded.", innerException);
}
