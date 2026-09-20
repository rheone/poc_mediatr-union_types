namespace MediatrUnionPoc.Api.Http;

/// <summary>
/// Stamps every response with an <c>X-Trace-Id</c> header and opens a logging scope carrying the
/// same value as <c>TraceId</c>, so every log line written while the request runs is correlated.
/// </summary>
/// <param name="next">The next delegate in the pipeline.</param>
/// <param name="logger">The logger the scope is opened on.</param>
public sealed class TraceIdMiddleware(RequestDelegate next, ILogger<TraceIdMiddleware> logger)
{
    /// <summary>Runs the request inside the trace-id scope.</summary>
    /// <param name="context">The current request context.</param>
    /// <returns>A task that completes when the rest of the pipeline has.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var traceId = context.TraceId;

        // OnStarting (not a direct header write) so the header survives the exception handler
        // clearing the response before it writes its problem body.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HttpContextTraceExtensions.TraceIdHeaderName] = traceId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object> { ["TraceId"] = traceId }))
        {
            await next(context);
        }
    }
}
