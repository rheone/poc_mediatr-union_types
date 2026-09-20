using System.Reflection;
using MediatrUnionPoc.Api.Health;
using MediatrUnionPoc.Api.Http;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;

namespace MediatrUnionPoc.Api.Logging;

/// <summary>Registration and pipeline placement for Serilog, which sits behind <see cref="Microsoft.Extensions.Logging.ILogger"/> in the Api host only.</summary>
public static class LoggingExtensions
{
    /// <summary>
    /// Makes Serilog the logging implementation behind <see cref="Microsoft.Extensions.Logging.ILogger"/>.
    /// Levels, overrides, sinks and the machine, process and thread enrichers come from the
    /// <c>Serilog</c> configuration section; the application name, version and environment come
    /// from the host, and the log context and any <see cref="Serilog.Core.ILogEventSink"/> registered
    /// in DI are added on top.
    /// </summary>
    /// <remarks>
    /// The logger is built per host from that host's configuration and services and is not assigned to
    /// the static <c>Log.Logger</c> (<c>preserveStaticLogger</c>), so several hosts in one process,
    /// as in the integration tests, never replace each other's logger. The request-logging middleware
    /// is handed the host's own logger for the same reason.
    /// </remarks>
    /// <param name="host">The host builder to configure.</param>
    /// <returns><paramref name="host"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> is <see langword="null"/>.</exception>
    public static IHostBuilder UseApiLogging(this IHostBuilder host)
    {
        ArgumentNullException.ThrowIfNull(host);

        var version =
            typeof(LoggingExtensions)
                .Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
            ?? typeof(LoggingExtensions).Assembly.GetName().Version?.ToString()
            ?? "unknown";

        return host.UseSerilog(
            (context, services, configuration) =>
                configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext()
                    .Enrich.WithProperty("Application", context.HostingEnvironment.ApplicationName)
                    .Enrich.WithProperty("ApplicationVersion", version)
                    .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName),
            preserveStaticLogger: true
        );
    }

    /// <summary>
    /// Adds the one-line-per-request log (method, path, status, elapsed milliseconds, plus
    /// <c>TraceId</c> and the caller's <see cref="RequestLogProperties"/>) and the middleware that puts the
    /// caller's properties on every other event of the request. Place it after
    /// <see cref="TraceIdMiddleware"/> and before the exception handler, and call
    /// <see cref="UseUserLogContext"/> after authentication.
    /// </summary>
    /// <remarks>
    /// Being outside the exception handler, the request line reports the final status of a request
    /// whose exception the handler already logged once at Error, so a handled exception is not
    /// logged twice. Health probes are written at Debug so they are silent at the default level.
    /// </remarks>
    /// <param name="app">The application builder.</param>
    /// <returns><paramref name="app"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is <see langword="null"/>.</exception>
    public static IApplicationBuilder UseApiRequestLogging(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseSerilogRequestLogging(options =>
        {
            options.Logger = app.ApplicationServices.GetRequiredService<Serilog.ILogger>();
            options.GetLevel = GetRequestLevel;
            options.EnrichDiagnosticContext = (diagnosticContext, http) =>
            {
                diagnosticContext.Set(RequestLogProperties.TraceId, http.TraceId);
                foreach (var property in RequestLogProperties.Describe(http.User))
                {
                    diagnosticContext.Set(property.Key, property.Value);
                }
            };
        });
    }

    /// <summary>Adds <see cref="UserLogContextMiddleware"/>; it must come after <c>UseAuthentication</c>.</summary>
    /// <param name="app">The application builder.</param>
    /// <returns><paramref name="app"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is <see langword="null"/>.</exception>
    public static IApplicationBuilder UseUserLogContext(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<UserLogContextMiddleware>();
    }

    private static LogEventLevel GetRequestLevel(
        HttpContext http,
        double elapsedMilliseconds,
        Exception? exception
    )
    {
        if (exception is not null)
        {
            return LogEventLevel.Error;
        }

        if (IsHealthProbe(http))
        {
            return LogEventLevel.Debug;
        }

        // An unhandled exception is logged once, at Error, by GlobalExceptionHandler; the request
        // line for that 500 must not be a second Error.
        if (http.Response.StatusCode < StatusCodes.Status500InternalServerError)
        {
            return LogEventLevel.Information;
        }

        // A request the timeout middleware answered 504 is a handled outcome, logged once at Warning by
        // RequestTimeoutResponseWriter; its request line is not a second, Error-level, entry.
        if (http.Response.StatusCode == StatusCodes.Status504GatewayTimeout)
        {
            return LogEventLevel.Warning;
        }

        return http.Features.Get<IExceptionHandlerFeature>() is null
            ? LogEventLevel.Error
            : LogEventLevel.Warning;
    }

    private static bool IsHealthProbe(HttpContext http)
    {
        var options = http
            .RequestServices.GetRequiredService<IOptions<HealthEndpointsOptions>>()
            .Value;

        return http.Request.Path.Equals(options.LivePath, StringComparison.OrdinalIgnoreCase)
            || http.Request.Path.Equals(options.ReadyPath, StringComparison.OrdinalIgnoreCase);
    }
}
