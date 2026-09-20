namespace MediatrUnionPoc.Api.Http;

/// <summary>DI registration for the union-to-HTTP mapping policy.</summary>
public static class ResultHttpMappingServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="HttpMappingOptions"/> (through the options pattern) for the extension
    /// members in <see cref="ResultHttpExtensions"/> to consult. Safe to call more than once; every
    /// <paramref name="configure"/> delegate is applied in order.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configure">Optional policy customisation, e.g. mapping an error code to a status.</param>
    /// <returns><paramref name="services"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddResultHttpMapping(
        this IServiceCollection services,
        Action<HttpMappingOptions>? configure = null
    )
    {
        ArgumentNullException.ThrowIfNull(services);

        var builder = services.AddOptions<HttpMappingOptions>();

        if (configure is not null)
        {
            builder.Configure(configure);
        }

        return services;
    }
}
