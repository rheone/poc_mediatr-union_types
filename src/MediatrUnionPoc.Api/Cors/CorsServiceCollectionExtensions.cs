using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;

namespace MediatrUnionPoc.Api.Cors;

/// <summary>Registration and pipeline placement for the API's single CORS policy.</summary>
public static class CorsServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ApiCorsOptions"/> (bound from <c>Cors</c>, validated on start) and the
    /// framework's CORS services with one default policy built from those options. The policy is applied
    /// to every request by <see cref="UseApiCors"/>; nothing uses <c>[EnableCors]</c>. With no allowed
    /// origin the policy allows nothing, so no CORS header is sent.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns><paramref name="services"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddApiCors(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddOptions<ApiCorsOptions>()
            .BindConfiguration(ApiCorsOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<ApiCorsOptions>, ApiCorsOptionsValidator>();

        services.AddCors();
        services
            .AddOptions<CorsOptions>()
            .Configure<IOptions<ApiCorsOptions>>(
                (cors, api) => cors.AddDefaultPolicy(BuildPolicy(api.Value))
            );

        return services;
    }

    /// <summary>
    /// Adds the CORS middleware with the default policy. Place it after routing and HTTPS redirection and
    /// before <c>UseAuthentication</c>: a preflight (<c>OPTIONS</c> with <c>Access-Control-Request-Method</c>)
    /// carries no credentials, so it must be answered here, before authentication and the fallback
    /// authorization policy can refuse it with a 401.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns><paramref name="app"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is <see langword="null"/>.</exception>
    public static IApplicationBuilder UseApiCors(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseCors();
    }

    private static CorsPolicy BuildPolicy(ApiCorsOptions options)
    {
        var builder = new CorsPolicyBuilder()
            .WithOrigins([.. options.AllowedOrigins])
            .WithMethods([.. options.GetAllowedMethods()])
            .WithHeaders([.. options.GetAllowedHeaders()])
            .WithExposedHeaders([.. options.GetExposedHeaders()])
            .SetPreflightMaxAge(TimeSpan.FromSeconds(options.PreflightMaxAgeSeconds));

        if (options.AllowCredentials)
        {
            builder.AllowCredentials();
        }

        return builder.Build();
    }
}
