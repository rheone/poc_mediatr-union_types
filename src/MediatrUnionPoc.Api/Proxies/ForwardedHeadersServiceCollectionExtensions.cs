using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace MediatrUnionPoc.Api.Proxies;

/// <summary>Registration and pipeline placement for the opt-in forwarded-headers handling.</summary>
public static class ForwardedHeadersServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ApiForwardedHeadersOptions"/> (bound from <c>ForwardedHeaders</c>, validated on
    /// start) and, when trusted proxies are configured, the framework's <c>ForwardedHeadersOptions</c>:
    /// <c>X-Forwarded-For</c> and <c>X-Forwarded-Proto</c>, honoured only from exactly those proxies (the
    /// framework's default trust of loopback is removed).
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns><paramref name="services"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddApiForwardedHeaders(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddOptions<ApiForwardedHeadersOptions>()
            .BindConfiguration(ApiForwardedHeadersOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<
            IValidateOptions<ApiForwardedHeadersOptions>,
            ApiForwardedHeadersOptionsValidator
        >();

        services
            .AddOptions<ForwardedHeadersOptions>()
            .Configure<IOptions<ApiForwardedHeadersOptions>>(
                (forwarded, api) =>
                {
                    if (api.Value.TrustedProxies.Length == 0)
                    {
                        return;
                    }

                    forwarded.ForwardedHeaders =
                        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                    forwarded.KnownProxies.Clear();
                    forwarded.KnownIPNetworks.Clear();

                    foreach (var entry in api.Value.TrustedProxies)
                    {
                        if (
                            !ApiForwardedHeadersOptionsValidator.TryParse(
                                entry,
                                out var address,
                                out var network
                            )
                        )
                        {
                            continue;
                        }

                        if (address is not null)
                        {
                            forwarded.KnownProxies.Add(address);
                        }
                        else if (network is { } trusted)
                        {
                            forwarded.KnownIPNetworks.Add(trusted);
                        }
                    }
                }
            );

        return services;
    }

    /// <summary>
    /// Adds the framework's forwarded-headers middleware, but only when trusted proxies are configured;
    /// with none it adds nothing and client-supplied <c>X-Forwarded-*</c> headers are ignored. Place it
    /// first in the pipeline: everything after it (rate limiting, request logging, the audit
    /// <c>sourceIp</c>) then sees the client's address instead of the proxy's.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns><paramref name="app"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is <see langword="null"/>.</exception>
    public static IApplicationBuilder UseApiForwardedHeaders(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var trusted = app
            .ApplicationServices.GetRequiredService<IOptions<ApiForwardedHeadersOptions>>()
            .Value.TrustedProxies;

        return trusted.Length == 0 ? app : app.UseForwardedHeaders();
    }
}
