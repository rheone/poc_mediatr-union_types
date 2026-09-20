using MediatrUnionPoc.Api.Http;
using MediatrUnionPoc.Application.Features.Impersonation.IssueToken;
using Microsoft.Extensions.Options;

namespace MediatrUnionPoc.Api.Impersonation;

/// <summary>Registration for impersonation.</summary>
public static class ImpersonationServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ImpersonationOptions"/> (bound from <c>Impersonation</c>, validated on
    /// start), the host's <see cref="IImpersonationSettings"/> over them, the JWT-signing
    /// <see cref="IImpersonationTokenIssuer"/>, and the mapping of the disabled outcome
    /// (<see cref="ImpersonationErrors.DisabledCode"/>) to a 404. The bearer scheme reads the same
    /// options to accept tokens signed with the impersonation key.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns><paramref name="services"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddImpersonation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddOptions<ImpersonationOptions>()
            .BindConfiguration(ImpersonationOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<
            IValidateOptions<ImpersonationOptions>,
            ImpersonationOptionsValidator
        >();
        services.AddSingleton<IValidateOptions<ImpersonationOptions>, ImpersonationOptionsRules>();

        services.AddSingleton<IImpersonationSettings>(provider =>
            provider.GetRequiredService<IOptions<ImpersonationOptions>>().Value
        );
        services.AddSingleton<IImpersonationTokenIssuer, JwtImpersonationTokenIssuer>();

        services.Configure<HttpMappingOptions>(options =>
            options.ErrorStatusCodes[ImpersonationErrors.DisabledCode] =
                StatusCodes.Status404NotFound
        );

        return services;
    }
}
