using System.Runtime.CompilerServices;
using MediatR;
using MediatrUnionPoc.Application.Common.Abstractions;
using MediatrUnionPoc.Application.Common.Auditing;
using Microsoft.Extensions.Logging;

namespace MediatrUnionPoc.Application.Common.Behaviors;

/// <summary>
/// Records one <see cref="AuditEvent"/> in the audit stream for every <see cref="IAuditableRequest{TResponse}"/>,
/// whatever the outcome. It sits right after <see cref="LoggingBehavior{TRequest,TResponse}"/> and so wraps
/// <see cref="AuthorizationBehavior{TRequest,TResponse}"/> and <see cref="ValidationBehavior{TRequest,TResponse}"/>:
/// a request refused by policy or by validation, which never reaches its handler, is still recorded.
/// </summary>
/// <remarks>
/// <para>
/// The outcome is the runtime case name of the union that came back (<c>NotAuthorized</c>,
/// <c>ProductDto</c>, ...), read the same way <see cref="LoggingBehavior{TRequest,TResponse}"/> reads it;
/// the behavior never interprets a case. Target, reason and extra facts come from the request itself
/// (<see cref="IAuditableRequest{TResponse}.DescribeAudit"/>). Who did it comes from the request's
/// principal: the actor is the real caller (the <c>act</c> subject of an impersonated principal) and the
/// effective id is the identity the request ran as.
/// </para>
/// <para>
/// <b>Failure policy.</b> A request that must not happen unrecorded declares
/// <see cref="AuditFailurePolicy.FailClosed"/>: if the event cannot be written the failure is logged at
/// Error and an <see cref="AuditWriteFailedException"/> is thrown, so the response the handler produced
/// (an impersonation token) is never delivered. A request that has already taken effect declares
/// <see cref="AuditFailurePolicy.BestEffort"/>: <see cref="TransactionBehavior{TRequest,TResponse}"/> sits
/// inside this behavior, so by the time the event is written the mutation has committed and cannot be
/// undone by failing the request; the failure is logged at Error and the response proceeds. The write
/// deliberately ignores the request's cancellation token, so a client that disconnects cannot cause a
/// committed action to go unrecorded.
/// </para>
/// <para>
/// An exception thrown by the rest of the pipeline is recorded with the outcome <c>Exception</c>
/// (best effort, so the original exception is never masked) and rethrown.
/// </para>
/// </remarks>
/// <typeparam name="TRequest">The auditable request type.</typeparam>
/// <typeparam name="TResponse">The request's response union type.</typeparam>
/// <param name="auditLog">The audit stream events are written to.</param>
/// <param name="context">Supplies the request's trace id and source address.</param>
/// <param name="clock">The clock events are stamped from.</param>
/// <param name="logger">The operational logger a failed write is reported to (never the event itself).</param>
/// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
public sealed partial class AuditBehavior<TRequest, TResponse>(
    IAuditLog auditLog,
    IAuditRequestContext context,
    TimeProvider clock,
    ILogger<AuditBehavior<TRequest, TResponse>> logger
) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, IAuditableRequest<TResponse>
    where TResponse : IUnion
{
    /// <summary>The outcome recorded when the rest of the pipeline threw instead of returning a response.</summary>
    public const string UnhandledOutcome = "Exception";

    private readonly IAuditLog _auditLog =
        auditLog ?? throw new ArgumentNullException(nameof(auditLog));

    private readonly IAuditRequestContext _context =
        context ?? throw new ArgumentNullException(nameof(context));

    private readonly TimeProvider _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    private readonly ILogger<AuditBehavior<TRequest, TResponse>> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> or <paramref name="next"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuditWriteFailedException">The request is <see cref="AuditFailurePolicy.FailClosed"/> and its event could not be written.</exception>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        TResponse response;
        try
        {
            response = await next(cancellationToken);
        }
        catch
        {
            await RecordAsync(
                request,
                UnhandledOutcome,
                request.DescribeUnhandledAudit(),
                AuditFailurePolicy.BestEffort
            );
            throw;
        }

        await RecordAsync(
            request,
            response.Value?.GetType().Name ?? "null",
            request.DescribeAudit(response),
            request.AuditFailurePolicy
        );

        return response;
    }

    private async Task RecordAsync(
        TRequest request,
        string outcome,
        AuditDescription description,
        AuditFailurePolicy policy
    )
    {
        var identity = AuditIdentity.From(request.AuditPrincipal);
        var auditEvent = new AuditEvent
        {
            Id = Guid.NewGuid(),
            Timestamp = _clock.GetUtcNow(),
            Action = request.AuditAction,
            Outcome = outcome,
            ActorId = AuditText.Limit(identity.ActorId),
            EffectiveId = AuditText.Limit(identity.EffectiveId),
            IsImpersonated = identity.IsImpersonated,
            TokenId = AuditText.Limit(identity.TokenId ?? description.TokenId),
            TargetType = AuditText.Limit(description.TargetType),
            TargetId = AuditText.Limit(description.TargetId),
            Reason = AuditText.Limit(description.Reason),
            Ticket = AuditText.Limit(description.Ticket),
            TraceId = _context.TraceId,
            SourceIp = _context.SourceIp,
            Details = (description.Details ?? new Dictionary<string, string>()).ToDictionary(
                pair => AuditText.Limit(pair.Key),
                pair => AuditText.Limit(pair.Value),
                StringComparer.Ordinal
            ),
        };

        try
        {
            // Not the request's token: a client that disconnects must not cost a committed action its record.
            await _auditLog.RecordAsync(auditEvent, CancellationToken.None);
        }
        catch (Exception ex)
        {
            LogWriteFailed(ex, auditEvent.Action, auditEvent.Id, typeof(TRequest).Name, policy);

            if (policy == AuditFailurePolicy.FailClosed)
            {
                throw AuditWriteFailedException.ForAction(auditEvent.Action, ex);
            }
        }
    }

    // Event ids 1100 to 1199 belong to this behavior. Only the action and the event id are logged,
    // never the event's content: the audit stream and the operational log stay separate.
    [LoggerMessage(
        EventId = 1100,
        EventName = "AuditWriteFailed",
        Level = LogLevel.Error,
        Message = "The audit event {AuditEventId} for {AuditAction} ({RequestName}) could not be written; policy {AuditFailurePolicy}"
    )]
    private partial void LogWriteFailed(
        Exception exception,
        string auditAction,
        Guid auditEventId,
        string requestName,
        AuditFailurePolicy auditFailurePolicy
    );
}
