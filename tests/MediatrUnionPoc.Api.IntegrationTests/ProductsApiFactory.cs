using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>
/// Boots the real ASP.NET Core host (real DI container, real MediatR pipeline, real controller
/// routing, real SQLite). Nothing is substituted: with no <c>ConnectionStrings:Products</c> value
/// each host gets its own private in-memory SQLite database (kept alive by one open connection for
/// the host's lifetime) whose schema is created at startup, so tests using their own factory never
/// see another test's data.
/// </summary>
/// <remarks>
/// <para>
/// By default (<see cref="ApiAuthentication.TestScheme"/>) the header-driven
/// <see cref="TestAuthenticationHandler"/> is registered as the default authentication scheme, so
/// tests say who is calling with <see cref="TestIdentityExtensions.AsUser(HttpClient, string?, string[])"/>.
/// The production JWT bearer scheme stays registered, and with
/// <see cref="ApiAuthentication.RealJwt"/> it is the default; the fallback authorization policy and
/// every other piece of the pipeline are the real ones in both modes.
/// </para>
/// <para>
/// Logging is the real Serilog setup too, with two test-only configuration overrides: the file and
/// console sinks are restricted to <c>Fatal</c> (the rolling file sink opens its file on the first
/// write, so no <c>logs/</c> file is ever created and the test output stays quiet), and a
/// <see cref="CapturingLogEventSink"/> is registered so tests read what was logged from
/// <see cref="LogSink"/>. The sink is shared with hosts derived through <c>WithWebHostBuilder</c>.
/// </para>
/// <para>
/// Every host writes its audit stream to its own directory under the system temp path
/// (<see cref="AuditDirectory"/>), never under the repository; <see cref="ReadAuditEvents()"/> reads it
/// back, and the directory is removed when the factory is disposed (and, for a factory a test forgot
/// to dispose, when the test process exits).
/// </para>
/// </remarks>
/// <param name="authentication">Which scheme answers requests.</param>
public sealed class ProductsApiFactory(
    ApiAuthentication authentication = ApiAuthentication.TestScheme
) : WebApplicationFactory<Program>
{
    private static readonly string AuditRoot = Path.Combine(
        Path.GetTempPath(),
        "mediatr-union-poc-audit-tests",
        Guid.NewGuid().ToString("N")
    );

    static ProductsApiFactory() =>
        AppDomain.CurrentDomain.ProcessExit += (_, _) => DeleteQuietly(AuditRoot);

    /// <summary>Gets the directory this host's audit files are written to (created by the first write).</summary>
    public string AuditDirectory { get; } = Path.Combine(AuditRoot, Guid.NewGuid().ToString("N"));

    /// <summary>Gets everything the host (and any host derived from it) logged.</summary>
    internal CapturingLogEventSink LogSink { get; } = new();

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseSetting("Audit:Directory", AuditDirectory);
        builder.UseSetting("Serilog:WriteTo:File:Args:restrictedToMinimumLevel", "Fatal");
        builder.UseSetting("Serilog:WriteTo:Console:Args:restrictedToMinimumLevel", "Fatal");

        // Generous limits, so no ordinary test ever meets the limiter; the rate-limiting tests override
        // these through WithWebHostBuilder with tiny limits and very long windows.
        foreach (var policy in new[] { "Reads", "Writes", "Impersonation" })
        {
            builder.UseSetting($"RateLimiting:{policy}:PermitLimit", GenerousPermitLimit);
        }

        // A generous timeout, so no ordinary test ever meets it; the timeout tests override these through
        // WithWebHostBuilder with tens of milliseconds.
        builder.UseSetting("RequestTimeouts:Default", GenerousTimeout);
        builder.UseSetting("RequestTimeouts:Impersonation", GenerousTimeout);

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<ILogEventSink>(LogSink);
            services.AddSingleton<IStartupFilter, RemoteAddressStartupFilter>();
        });

        if (authentication == ApiAuthentication.TestScheme)
        {
            builder.ConfigureTestServices(services =>
                services
                    .AddAuthentication(options =>
                        options.DefaultScheme = TestAuthenticationHandler.SchemeName
                    )
                    .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                        TestAuthenticationHandler.SchemeName,
                        _ => { }
                    )
            );
        }
    }

    /// <summary>The per-window permit limit every policy gets unless a test overrides it (the largest the options allow).</summary>
    public const string GenerousPermitLimit = "1000000";

    /// <summary>The request timeout every policy gets unless a test overrides it (the largest the options allow).</summary>
    public const string GenerousTimeout = "00:10:00";

    /// <summary>A request header that overrides <see cref="TestRemoteAddress"/> for that one request, so a test can be several distinct network callers.</summary>
    public const string RemoteAddressHeaderName = "X-Test-Remote-Address";

    /// <summary>The source address every request appears to come from (the in-memory test server has none of its own), documentation range TEST-NET-3.</summary>
    public const string TestRemoteAddress = "203.0.113.7";

    /// <summary>Reads every audit event this host wrote, oldest file first and in line order within a file.</summary>
    /// <returns>The events as JSON objects, in the camelCase shape they were written in.</returns>
    public IReadOnlyList<JsonObject> ReadAuditEvents() => ReadAuditEvents(AuditDirectory);

    /// <summary>Reads every audit event written to <paramref name="directory"/>.</summary>
    /// <param name="directory">The audit directory.</param>
    /// <returns>The events as JSON objects.</returns>
    public static IReadOnlyList<JsonObject> ReadAuditEvents(string directory) =>
        [.. ReadAuditLines(directory).Select(line => JsonNode.Parse(line)!.AsObject())];

    /// <summary>Reads the raw text of every line written to <paramref name="directory"/>'s audit files.</summary>
    /// <param name="directory">The audit directory.</param>
    /// <returns>The lines, exactly as stored.</returns>
    public static IReadOnlyList<string> ReadAuditLines(string directory) =>
        !Directory.Exists(directory)
            ? []
            :
            [
                .. Directory
                    .EnumerateFiles(directory, "audit-*.jsonl")
                    .Order(StringComparer.Ordinal)
                    .SelectMany(File.ReadAllLines),
            ];

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            DeleteQuietly(AuditDirectory);
        }
    }

    private static void DeleteQuietly(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort temp cleanup; a locked file is left for the OS temp sweep.
        }
        catch (UnauthorizedAccessException)
        {
            // Same: never fail a test over cleanup.
        }
    }

    private sealed class RemoteAddressStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            app =>
            {
                app.Use(
                    (context, nextMiddleware) =>
                    {
                        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(
                            context.Request.Headers.TryGetValue(
                                RemoteAddressHeaderName,
                                out var over
                            )
                                ? over.ToString()
                                : TestRemoteAddress
                        );
                        return nextMiddleware(context);
                    }
                );
                next(app);
            };
    }
}
