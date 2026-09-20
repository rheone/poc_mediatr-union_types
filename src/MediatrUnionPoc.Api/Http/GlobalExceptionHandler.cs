using Microsoft.AspNetCore.Diagnostics;

namespace MediatrUnionPoc.Api.Http;

/// <summary>
/// The last-resort handler for exceptions nothing else caught. Expected outcomes are unions and
/// never reach here, so there is deliberately no per-exception-type status mapping: everything is
/// a 500. The exception is logged with its structured properties (the trace id comes from the
/// ambient logging scope opened by <see cref="TraceIdMiddleware"/>); the response never exposes
/// exception details outside the Development environment.
/// </summary>
/// <param name="problemDetails">Writes the RFC 7807 body (and applies the shared <c>traceId</c> customisation).</param>
/// <param name="environment">Decides whether the exception message may be echoed in the body.</param>
/// <param name="logger">The logger the exception is recorded on.</param>
public sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetails,
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger
) : IExceptionHandler
{
    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="httpContext"/> or <paramref name="exception"/> is <see langword="null"/>.</exception>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        // The client hung up: nobody is listening for a body and it is not a server fault.
        if (
            exception is OperationCanceledException
            && httpContext.RequestAborted.IsCancellationRequested
        )
        {
            LogClientAborted(httpContext.Request.Method, httpContext.Request.Path.Value);
            return true;
        }

        LogUnhandled(exception, httpContext.Request.Method, httpContext.Request.Path.Value);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await problemDetails.TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails =
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "An unexpected error occurred.",
                    Detail = environment.IsDevelopment() ? exception.ToString() : null,
                },
            }
        );
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Unhandled exception processing {RequestMethod} {RequestPath}"
    )]
    private partial void LogUnhandled(
        Exception exception,
        string requestMethod,
        string? requestPath
    );

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Client aborted {RequestMethod} {RequestPath}; request cancelled"
    )]
    private partial void LogClientAborted(string requestMethod, string? requestPath);
}
