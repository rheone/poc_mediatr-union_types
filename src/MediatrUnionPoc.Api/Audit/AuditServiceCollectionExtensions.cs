using MediatrUnionPoc.Application.Common.Auditing;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace MediatrUnionPoc.Api.Audit;

/// <summary>Registration and pipeline placement for the audit stream.</summary>
public static class AuditServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="AuditOptions"/> (bound from <c>Audit</c>, validated on start), the
    /// file-backed <see cref="IAuditLog"/> writing to the configured directory (a relative directory is
    /// resolved against the content root), and the <see cref="IAuditRequestContext"/> that supplies the
    /// request's trace id and source address. Call it after <c>AddApplication</c>, which registers the
    /// pipeline behavior that uses them.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns><paramref name="services"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddAudit(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddOptions<AuditOptions>()
            .BindConfiguration(AuditOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<AuditOptions>, AuditOptionsValidator>();

        services.AddSingleton(provider =>
        {
            var directory = provider.GetRequiredService<IOptions<AuditOptions>>().Value.Directory;
            var contentRoot = provider.GetRequiredService<IHostEnvironment>().ContentRootPath;

            return new FileAuditLog(Path.GetFullPath(directory, contentRoot));
        });
        services.AddSingleton<IAuditLog>(provider => provider.GetRequiredService<FileAuditLog>());

        services.AddHttpContextAccessor();
        services.Replace(
            ServiceDescriptor.Singleton<IAuditRequestContext, HttpAuditRequestContext>()
        );

        return services;
    }

    /// <summary>
    /// Adds <see cref="ImpersonationAuditMiddleware"/>, which records every request made under an
    /// impersonation token. Place it after <c>UseAuthentication</c> and before <c>UseAuthorization</c>, so a
    /// request an impersonated caller is then refused (403) is recorded too.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns><paramref name="app"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is <see langword="null"/>.</exception>
    public static IApplicationBuilder UseImpersonationAudit(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<ImpersonationAuditMiddleware>();
    }
}
