using Serilog.Context;
using Serilog.Core;
using Serilog.Core.Enrichers;

namespace MediatrUnionPoc.Api.Logging;

/// <summary>
/// Pushes the caller's <see cref="RequestLogProperties"/> onto the Serilog log context for the rest
/// of the request, so every event written downstream (the MediatR behaviors, handlers, the exception
/// handler) carries <c>UserId</c>, <c>IsImpersonated</c> and <c>ImpersonatedBy</c>. It must run after
/// authentication, which is what populates <see cref="HttpContext.User"/>.
/// </summary>
/// <param name="next">The next delegate in the pipeline.</param>
public sealed class UserLogContextMiddleware(RequestDelegate next)
{
    /// <summary>Runs the rest of the pipeline with the caller's properties in the log context.</summary>
    /// <param name="context">The current request context.</param>
    /// <returns>A task that completes when the rest of the pipeline has.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var enrichers = RequestLogProperties
            .Describe(context.User)
            .Select(property =>
                (ILogEventEnricher)new PropertyEnricher(property.Key, property.Value)
            )
            .ToArray();

        using (LogContext.Push(enrichers))
        {
            await next(context);
        }
    }
}
