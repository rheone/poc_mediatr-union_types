using Microsoft.AspNetCore.Authentication;
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
/// </remarks>
/// <param name="authentication">Which scheme answers requests.</param>
public sealed class ProductsApiFactory(
    ApiAuthentication authentication = ApiAuthentication.TestScheme
) : WebApplicationFactory<Program>
{
    /// <summary>Gets everything the host (and any host derived from it) logged.</summary>
    internal CapturingLogEventSink LogSink { get; } = new();

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseSetting("Serilog:WriteTo:File:Args:restrictedToMinimumLevel", "Fatal");
        builder.UseSetting("Serilog:WriteTo:Console:Args:restrictedToMinimumLevel", "Fatal");
        builder.ConfigureTestServices(services => services.AddSingleton<ILogEventSink>(LogSink));

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
}
