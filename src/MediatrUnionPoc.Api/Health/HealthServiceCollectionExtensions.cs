using MediatrUnionPoc.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace MediatrUnionPoc.Api.Health;

/// <summary>Registration and endpoint mapping for the liveness and readiness probes.</summary>
public static class HealthServiceCollectionExtensions
{
    /// <summary>The tag that marks a health check as part of readiness.</summary>
    public const string ReadyTag = "ready";

    /// <summary>
    /// Registers <see cref="HealthEndpointsOptions"/> (bound from <c>HealthEndpoints</c>, validated on
    /// start) and the health checks: a database check on <see cref="AppDbContext"/> tagged
    /// <see cref="ReadyTag"/>. Liveness has no check of its own.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns><paramref name="services"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddHealthEndpoints(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddOptions<HealthEndpointsOptions>()
            .BindConfiguration(HealthEndpointsOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<
            IValidateOptions<HealthEndpointsOptions>,
            HealthEndpointsOptionsValidator
        >();

        // The default probe (CanConnectAsync) short-circuits for a shared in-memory SQLite database
        // without opening a connection; a real round trip proves the connection works.
        services
            .AddHealthChecks()
            .AddDbContextCheck<AppDbContext>(
                customTestQuery: (dbContext, cancellationToken) =>
                    dbContext.Database.SqlQueryRaw<int>("SELECT 1").AnyAsync(cancellationToken),
                tags: [ReadyTag]
            );

        return services;
    }

    /// <summary>
    /// Maps the anonymous, rate-limit-exempt (a probe must never be refused) and timeout-exempt (the orchestrator owns the probe's deadline, and a 504 from the API would misreport a stalled dependency as the probe's own failure) liveness endpoint (no checks; proves the process answers) and readiness
    /// endpoint (checks tagged <see cref="ReadyTag"/>). Both answer with the default plain-text
    /// status only: <c>Healthy</c>, <c>Degraded</c> (200) or <c>Unhealthy</c> (503).
    /// </summary>
    /// <param name="endpoints">The endpoint route builder to map onto.</param>
    /// <returns><paramref name="endpoints"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="endpoints"/> is <see langword="null"/>.</exception>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var options = endpoints
            .ServiceProvider.GetRequiredService<IOptions<HealthEndpointsOptions>>()
            .Value;

        endpoints
            .MapHealthChecks(options.LivePath, new HealthCheckOptions { Predicate = _ => false })
            .AllowAnonymous()
            .DisableRateLimiting()
            .DisableRequestTimeout();
        endpoints
            .MapHealthChecks(
                options.ReadyPath,
                new HealthCheckOptions { Predicate = check => check.Tags.Contains(ReadyTag) }
            )
            .AllowAnonymous()
            .DisableRateLimiting()
            .DisableRequestTimeout();

        return endpoints;
    }
}
