using MediatrUnionPoc.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MediatrUnionPoc.Infrastructure;

/// <summary>Wires up EF Core (SQLite) and the repository/unit-of-work implementations.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers <see cref="AppDbContext"/> and its dependents as <c>Scoped</c> so a single HTTP
    /// request's repositories and <see cref="EfCoreUnitOfWork"/> share one tracked context —
    /// required for <see cref="EfCoreUnitOfWork"/>'s commit/rollback semantics to see the same
    /// staged changes the repositories made.
    /// </summary>
    /// <remarks>
    /// The database is SQLite. The <c>ConnectionStrings:Products</c> configuration value selects it
    /// (read from the <see cref="IConfiguration"/> registered in the container, if any); with no such
    /// value each service provider gets its own private in-memory database, kept alive by one open
    /// connection for the provider's lifetime, so nothing needs setting up. The schema is not created
    /// here; call <see cref="EnsureInfrastructureCreatedAsync"/> once at startup.
    /// </remarks>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The same <paramref name="services"/> collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(serviceProvider => new ProductsDatabase(
            serviceProvider.GetService<IConfiguration>()?.GetConnectionString("Products")
        ));
        services.AddDbContext<AppDbContext>(
            (serviceProvider, options) =>
                options.UseSqlite(
                    serviceProvider.GetRequiredService<ProductsDatabase>().ConnectionString
                )
        );
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        return services;
    }

    /// <summary>
    /// Creates the database schema (<c>EnsureCreated</c>; this POC has no migrations) if it does not
    /// exist yet. Call once at startup, before serving requests.
    /// </summary>
    /// <param name="serviceProvider">The root service provider of an app that called <see cref="AddInfrastructure"/>.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous creation.</param>
    /// <returns>A task representing the asynchronous creation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="serviceProvider"/> is <see langword="null"/>.</exception>
    public static async Task EnsureInfrastructureCreatedAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
    }
}
