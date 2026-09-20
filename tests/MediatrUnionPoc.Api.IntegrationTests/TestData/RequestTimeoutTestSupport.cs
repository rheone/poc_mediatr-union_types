using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace MediatrUnionPoc.Api.IntegrationTests.TestData;

/// <summary>Shared arrangement for the request-timeout tests: a timeout is always tens of milliseconds and no test waits on the wall clock for a result.</summary>
public static class RequestTimeoutTestSupport
{
    /// <summary>The <c>code</c> member every timeout problem carries.</summary>
    public const string RequestTimeoutCode = "REQUEST_TIMEOUT";

    /// <summary>The tiny timeout the tests configure.</summary>
    public static readonly TimeSpan Tiny = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Derives a host whose timeout policies have the given values; a policy left <see langword="null"/>
    /// keeps the factory's generous default.
    /// </summary>
    /// <param name="factory">The host to derive from.</param>
    /// <param name="timeout">The default timeout.</param>
    /// <param name="impersonation">The <c>Impersonation</c> policy's timeout.</param>
    /// <param name="configure">Any further host configuration.</param>
    /// <returns>The derived host; the caller disposes it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <see langword="null"/>.</exception>
    public static WebApplicationFactory<Program> WithTimeouts(
        this WebApplicationFactory<Program> factory,
        TimeSpan? timeout = null,
        TimeSpan? impersonation = null,
        Action<IWebHostBuilder>? configure = null
    )
    {
        ArgumentNullException.ThrowIfNull(factory);

        return factory.WithWebHostBuilder(builder =>
        {
            Set(builder, "Default", timeout);
            Set(builder, "Impersonation", impersonation);
            configure?.Invoke(builder);
        });
    }

    /// <summary>Adds the throwaway <see cref="RequestTimeoutProbeController"/> to the host.</summary>
    /// <param name="builder">The host builder.</param>
    /// <returns><paramref name="builder"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IWebHostBuilder WithTimeoutProbe(this IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.ConfigureTestServices(services =>
            services
                .AddControllers()
                .AddApplicationPart(typeof(RequestTimeoutProbeController).Assembly)
        );
    }

    private static void Set(IWebHostBuilder builder, string policy, TimeSpan? value)
    {
        if (value is { } timeout)
        {
            builder.UseSetting(
                $"RequestTimeouts:{policy}",
                timeout.ToString("c", CultureInfo.InvariantCulture)
            );
        }
    }
}
