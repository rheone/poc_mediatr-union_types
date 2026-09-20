using System.Globalization;
using System.Threading.RateLimiting;
using MediatrUnionPoc.Api.Http;
using MediatrUnionPoc.Application.Common.Auditing;
using MediatrUnionPoc.Application.Features.Impersonation.IssueToken;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace MediatrUnionPoc.Api.RateLimiting;

/// <summary>
/// Answers a request the rate limiter refused: <c>429 Too Many Requests</c> as
/// <c>application/problem+json</c> with the trace id, a <c>code</c> member of
/// <see cref="ResultHttpExtensions.RateLimitedCode"/> and a <c>Retry-After</c> header in whole seconds.
/// The body says nothing about other callers or the limit's internals. A refusal is logged at Warning
/// (policy and partition kind, never the address), and a refused impersonation-token request is also
/// written to the audit stream, so "every attempt audited, including denials" holds for a caller that
/// never reached the command.
/// </summary>
/// <param name="problemDetails">Writes the problem body (and stamps the trace id through the shared customisation).</param>
/// <param name="httpMapping">Supplies the RFC 7807 <c>type</c> URI for the status.</param>
/// <param name="options">Supplies each policy's window, the <c>Retry-After</c> fallback.</param>
/// <param name="auditLog">The audit stream a refused impersonation request is recorded on.</param>
/// <param name="clock">The clock the audit event is stamped from.</param>
/// <param name="logger">The operational logger.</param>
/// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
public sealed partial class RateLimitRejectionHandler(
    IProblemDetailsService problemDetails,
    IOptions<HttpMappingOptions> httpMapping,
    IOptions<RateLimitingOptions> options,
    IAuditLog auditLog,
    TimeProvider clock,
    ILogger<RateLimitRejectionHandler> logger
)
{
    /// <summary>The audit outcome recorded when the limiter refused an impersonation-token request.</summary>
    public const string AuditOutcome = "RateLimited";

    private readonly IProblemDetailsService _problemDetails =
        problemDetails ?? throw new ArgumentNullException(nameof(problemDetails));

    private readonly HttpMappingOptions _httpMapping = (
        httpMapping ?? throw new ArgumentNullException(nameof(httpMapping))
    ).Value;

    private readonly RateLimitingOptions _options = (
        options ?? throw new ArgumentNullException(nameof(options))
    ).Value;

    private readonly IAuditLog _auditLog =
        auditLog ?? throw new ArgumentNullException(nameof(auditLog));

    private readonly TimeProvider _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    private readonly ILogger<RateLimitRejectionHandler> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>Writes the 429 for a refused request. Plugged in as the limiter's <c>OnRejected</c>.</summary>
    /// <param name="context">The refusal: the request and the lease the limiter denied.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the response has been written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public async ValueTask OnRejectedAsync(
        OnRejectedContext context,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(context);

        var http = context.HttpContext;
        var policy =
            http.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName
            ?? RateLimitPolicyNames.Reads;
        var caller = RateLimitCaller.From(http);
        var retryAfterSeconds = RetryAfterSeconds(context.Lease, policy);

        LogRejected(_logger, policy, caller.Kind, retryAfterSeconds);

        if (string.Equals(policy, RateLimitPolicyNames.Impersonation, StringComparison.Ordinal))
        {
            await RecordAuditAsync(http, policy);
        }

        http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        http.Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

        await _problemDetails.WriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = http,
                ProblemDetails =
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too Many Requests",
                    Detail = $"Rate limit exceeded. Retry after {retryAfterSeconds} seconds.",
                    Type = _httpMapping.TypeUriFor(StatusCodes.Status429TooManyRequests),
                    Extensions =
                    {
                        [ResultHttpExtensions.CodeExtensionName] =
                            ResultHttpExtensions.RateLimitedCode,
                    },
                },
            }
        );
    }

    private int RetryAfterSeconds(RateLimitLease lease, string policy)
    {
        if (lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            return Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
        }

        // The limiter gave no hint (a queue-full refusal can): the whole window is the honest upper bound.
        var current = _options;
        var window = policy switch
        {
            RateLimitPolicyNames.Writes => current.Writes.WindowSeconds,
            RateLimitPolicyNames.Impersonation => current.Impersonation.WindowSeconds,
            _ => current.Reads.WindowSeconds,
        };

        return Math.Max(1, window);
    }

    private async Task RecordAuditAsync(HttpContext http, string policy)
    {
        var identity = AuditIdentity.From(http.User);
        var auditEvent = new AuditEvent
        {
            Id = Guid.NewGuid(),
            Timestamp = _clock.GetUtcNow(),
            Action = IssueImpersonationTokenCommand.Action,
            Outcome = AuditOutcome,
            ActorId = AuditText.Limit(identity.ActorId),
            EffectiveId = AuditText.Limit(identity.EffectiveId),
            IsImpersonated = identity.IsImpersonated,
            TokenId = AuditText.Limit(identity.TokenId),
            TraceId = http.TraceId,
            SourceIp = http.Connection.RemoteIpAddress?.ToString(),
            Details = new Dictionary<string, string>
            {
                ["method"] = AuditText.Limit(http.Request.Method),
                ["path"] = AuditText.Limit(http.Request.Path.Value ?? string.Empty),
                ["policy"] = policy,
            },
        };

        try
        {
            // Not RequestAborted: a client that disconnects must not cost the attempt its record.
            await _auditLog.RecordAsync(auditEvent, CancellationToken.None);
        }
        catch (Exception ex)
        {
            LogAuditWriteFailed(_logger, ex, auditEvent.Action, auditEvent.Id);
        }
    }

    // Event ids 1300 to 1399 belong to rate limiting (1100s: AuditBehavior, 1200s: audit middleware).
    [LoggerMessage(
        EventId = 1300,
        EventName = "RateLimited",
        Level = LogLevel.Warning,
        Message = "Rate limit policy {RateLimitPolicy} refused a request from a {RateLimitPartitionKind} partition; retry after {RetryAfterSeconds} seconds"
    )]
    private static partial void LogRejected(
        ILogger logger,
        string rateLimitPolicy,
        string rateLimitPartitionKind,
        int retryAfterSeconds
    );

    [LoggerMessage(
        EventId = 1301,
        EventName = "RateLimitAuditWriteFailed",
        Level = LogLevel.Error,
        Message = "The audit event {AuditEventId} for {AuditAction} could not be written; the 429 response was still sent"
    )]
    private static partial void LogAuditWriteFailed(
        ILogger logger,
        Exception exception,
        string auditAction,
        Guid auditEventId
    );
}
