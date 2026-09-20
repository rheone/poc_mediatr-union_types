using MediatrUnionPoc.Api.Http;
using MediatrUnionPoc.Application.Common.Auditing;
using MediatrUnionPoc.Application.Common.Authorization;

namespace MediatrUnionPoc.Api.Audit;

/// <summary>
/// Writes an <see cref="ActionName"/> audit event for every request made under an impersonation token,
/// once the rest of the pipeline has produced its status: HTTP method, path (never the query string,
/// which may carry data), status code, the token's <c>jti</c> (so each request ties back to the event
/// that minted the token), the real caller, the identity it ran as, the recorded reason, the trace id and
/// the source address. Ordinary requests are not audited here, so health probes (anonymous) never appear.
/// It must run after authentication, which is what populates <see cref="HttpContext.User"/>.
/// </summary>
/// <remarks>
/// Best effort: by the time the event is written the response has already been produced and the work
/// done, so a write failure is logged at Error through <see cref="ILogger"/> (the event's content is not)
/// and the response is unaffected. A request that ends in an unhandled exception is recorded as 500,
/// the status the exception handler outside this middleware will answer with. Requests that never
/// authenticate (a rejected or missing token) have no principal to attribute and are not audited; they
/// remain visible in the operational request log.
/// </remarks>
/// <param name="next">The next delegate in the pipeline.</param>
public sealed partial class ImpersonationAuditMiddleware(RequestDelegate next)
{
    /// <summary>The audit action recorded for a request made under an impersonation token.</summary>
    public const string ActionName = "Impersonation.Request";

    /// <summary>Runs the request and, when it is impersonated, records it.</summary>
    /// <param name="context">The current request context.</param>
    /// <param name="auditLog">The audit stream.</param>
    /// <param name="clock">The clock the event is stamped from.</param>
    /// <param name="logger">The operational logger a failed write is reported to.</param>
    /// <returns>A task that completes when the rest of the pipeline has and the event is written.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public async Task InvokeAsync(
        HttpContext context,
        IAuditLog auditLog,
        TimeProvider clock,
        ILogger<ImpersonationAuditMiddleware> logger
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(auditLog);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        if (!context.User.IsImpersonated())
        {
            await next(context);
            return;
        }

        var statusCode = StatusCodes.Status500InternalServerError;
        try
        {
            await next(context);
            statusCode = context.Response.StatusCode;
        }
        finally
        {
            await RecordAsync(context, auditLog, clock, logger, statusCode);
        }
    }

    private static async Task RecordAsync(
        HttpContext context,
        IAuditLog auditLog,
        TimeProvider clock,
        ILogger logger,
        int statusCode
    )
    {
        var identity = AuditIdentity.From(context.User);
        var auditEvent = new AuditEvent
        {
            Id = Guid.NewGuid(),
            Timestamp = clock.GetUtcNow(),
            Action = ActionName,
            Outcome = statusCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ActorId = AuditText.Limit(identity.ActorId),
            EffectiveId = AuditText.Limit(identity.EffectiveId),
            IsImpersonated = true,
            TokenId = AuditText.Limit(identity.TokenId),
            Reason = AuditText.Limit(context.User.FindFirst(ImpersonationClaims.Reason)?.Value),
            Ticket = AuditText.Limit(context.User.FindFirst(ImpersonationClaims.Ticket)?.Value),
            TraceId = context.TraceId,
            SourceIp = context.Connection.RemoteIpAddress?.ToString(),
            Details = new Dictionary<string, string>
            {
                ["method"] = AuditText.Limit(context.Request.Method),
                ["path"] = AuditText.Limit(context.Request.Path.Value ?? string.Empty),
            },
        };

        try
        {
            // Not RequestAborted: a client that disconnects must not cost the request its record.
            await auditLog.RecordAsync(auditEvent, CancellationToken.None);
        }
        catch (Exception ex)
        {
            LogWriteFailed(logger, ex, auditEvent.Action, auditEvent.Id);
        }
    }

    // Event ids 1200 to 1299 belong to the audit middleware; 1100 to 1199 to AuditBehavior.
    [LoggerMessage(
        EventId = 1200,
        EventName = "AuditRequestWriteFailed",
        Level = LogLevel.Error,
        Message = "The audit event {AuditEventId} for {AuditAction} could not be written; the response was not affected"
    )]
    private static partial void LogWriteFailed(
        ILogger logger,
        Exception exception,
        string auditAction,
        Guid auditEventId
    );
}
