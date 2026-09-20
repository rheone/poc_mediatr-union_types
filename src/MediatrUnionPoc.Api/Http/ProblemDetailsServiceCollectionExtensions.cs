namespace MediatrUnionPoc.Api.Http;

/// <summary>DI registration for RFC 7807 problem bodies (including the framework's own) with the shared trace id.</summary>
public static class ProblemDetailsServiceCollectionExtensions
{
    /// <summary>
    /// Registers <c>AddProblemDetails</c> with a customisation that stamps every problem body with
    /// the <c>traceId</c> extension member from the <c>HttpContext.TraceId</c> extension member,
    /// replacing the framework default (which is the full <c>Activity.Id</c>) so the body agrees
    /// with the <c>X-Trace-Id</c> header and the logging scope.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns><paramref name="services"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddApiProblemDetails(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
                context.ProblemDetails.Extensions[HttpContextTraceExtensions.TraceIdName] = context
                    .HttpContext
                    .TraceId
        );
    }
}
