using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace MediatrUnionPoc.Api.Authentication;

/// <summary>Registration for JWT bearer authentication and the secure-by-default authorization policy.</summary>
public static class AuthenticationServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="JwtAuthOptions"/> (bound from <c>Authentication:Jwt</c>, validated on
    /// start), the JWT bearer scheme configured from them as the default scheme, the fallback
    /// authorization policy that makes every endpoint require an authenticated caller unless it opts
    /// out with <c>AllowAnonymous</c>, the opt-in Development-only <see cref="DevIdentityOptions"/>
    /// sign-in for tokenless requests, and <see cref="ProblemDetailsAuthorizationResultHandler"/> so
    /// the middleware's 401 and 403 are problem responses.
    /// </summary>
    /// <remarks>
    /// The Application layer already registered its own <c>Administrator</c> and
    /// <c>ProductOwner</c> policies through <c>AddAuthorizationCore</c>; <c>AddAuthorization</c> here
    /// adds the ASP.NET Core pieces (policy evaluator, middleware result handler) on top and layers
    /// the fallback policy onto the same <see cref="AuthorizationOptions"/>, so one
    /// <see cref="IAuthorizationService"/> and one policy set serve both the middleware and the
    /// MediatR pipeline.
    /// </remarks>
    /// <param name="services">The service collection to add to.</param>
    /// <returns><paramref name="services"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddOptions<JwtAuthOptions>()
            .BindConfiguration(JwtAuthOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtAuthOptions>, JwtAuthOptionsValidator>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();

        services
            .AddOptions<DevIdentityOptions>()
            .BindConfiguration(DevIdentityOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<DevIdentityOptions>, DevIdentityOptionsValidator>();
        services.AddSingleton<
            IValidateOptions<DevIdentityOptions>,
            DevIdentityEnvironmentValidator
        >();
        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureDevIdentity>();

        services.AddAuthorization(options =>
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build()
        );
        services.AddSingleton<
            IAuthorizationMiddlewareResultHandler,
            ProblemDetailsAuthorizationResultHandler
        >();

        return services;
    }
}
