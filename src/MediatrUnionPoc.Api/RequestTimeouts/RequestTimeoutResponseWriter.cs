using MediatrUnionPoc.Api.Http;
using Microsoft.Extensions.Options;

namespace MediatrUnionPoc.Api.RequestTimeouts;

/// <summary>
/// Answers a request whose timeout elapsed: <c>504 Gateway Timeout</c> as <c>application/problem+json</c>
/// with the trace id, a <c>code</c> member of <see cref="ResultHttpExtensions.RequestTimeoutCode"/> and the
/// RFC 7807 <c>type</c> URI from <see cref="HttpMappingOptions"/>. The body names no handler and no internal
/// state. Plugged in as the policies' <c>WriteTimeoutResponse</c>, so it runs only when the timeout
/// middleware caught the cancellation before any response byte was sent; a request that already started
/// its response is aborted by the framework instead. The timeout is logged once at Warning.
/// </summary>
/// <param name="problemDetails">Writes the problem body (and stamps the trace id through the shared customisation).</param>
/// <param name="httpMapping">Supplies the RFC 7807 <c>type</c> URI for the status.</param>
/// <param name="logger">The operational logger.</param>
/// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
public sealed partial class RequestTimeoutResponseWriter(
    IProblemDetailsService problemDetails,
    IOptions<HttpMappingOptions> httpMapping,
    ILogger<RequestTimeoutResponseWriter> logger
)
{
    private readonly IProblemDetailsService _problemDetails =
        problemDetails ?? throw new ArgumentNullException(nameof(problemDetails));

    private readonly HttpMappingOptions _httpMapping = (
        httpMapping ?? throw new ArgumentNullException(nameof(httpMapping))
    ).Value;

    private readonly ILogger<RequestTimeoutResponseWriter> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>Writes the 504 for a timed-out request.</summary>
    /// <param name="http">The timed-out request's context.</param>
    /// <returns>A task that completes when the response has been written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="http"/> is <see langword="null"/>.</exception>
    public async Task WriteAsync(HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(http);

        LogTimedOut(_logger, http.Request.Method, http.Request.Path.Value);

        http.Response.StatusCode = StatusCodes.Status504GatewayTimeout;

        await _problemDetails.WriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = http,
                ProblemDetails =
                {
                    Status = StatusCodes.Status504GatewayTimeout,
                    Title = "Gateway Timeout",
                    Detail = "The request did not complete in time and was cancelled.",
                    Type = _httpMapping.TypeUriFor(StatusCodes.Status504GatewayTimeout),
                    Extensions =
                    {
                        [ResultHttpExtensions.CodeExtensionName] =
                            ResultHttpExtensions.RequestTimeoutCode,
                    },
                },
            }
        );
    }

    // Event ids 1400 to 1499 belong to request timeouts (1300s: rate limiting).
    [LoggerMessage(
        EventId = 1400,
        EventName = "RequestTimedOut",
        Level = LogLevel.Warning,
        Message = "Request {RequestMethod} {RequestPath} exceeded its timeout and was cancelled"
    )]
    private static partial void LogTimedOut(
        ILogger logger,
        string requestMethod,
        string? requestPath
    );
}
