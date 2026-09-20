using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.Extensions.Options;

namespace MediatrUnionPoc.Api.RequestTimeouts;

/// <summary>Registration and pipeline placement for request timeouts.</summary>
public static class RequestTimeoutServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="RequestTimeoutOptions"/> (bound from <c>RequestTimeouts</c>, validated on start),
    /// the framework's request-timeouts services, a default policy that covers every endpoint that names no
    /// policy and does not opt out (so a new action is covered without an attribute), one named policy per
    /// name in <see cref="RequestTimeoutPolicyNames"/>, and the <see cref="RequestTimeoutResponseWriter"/> that
    /// answers a timed-out request with a 504 problem. The timeouts are read once, from
    /// <see cref="IOptions{TOptions}"/>: changing them takes a restart. Call it after
    /// <c>AddApiProblemDetails</c> and <c>AddResultHttpMapping</c>.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns><paramref name="services"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddApiRequestTimeouts(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddOptions<RequestTimeoutOptions>()
            .BindConfiguration(RequestTimeoutOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<
            IValidateOptions<RequestTimeoutOptions>,
            RequestTimeoutOptionsValidator
        >();
        services.AddSingleton<RequestTimeoutResponseWriter>();

        services.AddRequestTimeouts();
        services
            .AddOptions<Microsoft.AspNetCore.Http.Timeouts.RequestTimeoutOptions>()
            .Configure<IOptions<RequestTimeoutOptions>, RequestTimeoutResponseWriter>(
                (framework, settings, writer) =>
                {
                    var timeouts = settings.Value;

                    framework.DefaultPolicy = PolicyFor(timeouts.Default, writer);
                    framework.AddPolicy(
                        RequestTimeoutPolicyNames.Impersonation,
                        PolicyFor(timeouts.Impersonation, writer)
                    );
                }
            );

        return services;
    }

    /// <summary>
    /// Adds the timeout middleware. It needs the endpoint to be resolved (routing, implicit in the minimal
    /// host) to pick a policy, and must sit inside the exception handler and request logging (so the 504 is
    /// the status they report) and inside the impersonation audit (which then records the real 504 rather
    /// than a cancelled request's 500). Everything after it is covered: the rate limiter's queue wait,
    /// authorization and the action. Authentication, before it, is local JWT validation with no I/O.
    /// The framework does nothing while a debugger is attached.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns><paramref name="app"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is <see langword="null"/>.</exception>
    public static IApplicationBuilder UseApiRequestTimeouts(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseRequestTimeouts();
    }

    private static RequestTimeoutPolicy PolicyFor(
        TimeSpan timeout,
        RequestTimeoutResponseWriter writer
    ) =>
        new()
        {
            Timeout = timeout,
            TimeoutStatusCode = StatusCodes.Status504GatewayTimeout,
            WriteTimeoutResponse = writer.WriteAsync,
        };
}
