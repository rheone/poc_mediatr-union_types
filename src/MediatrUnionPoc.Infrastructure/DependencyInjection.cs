using MediatrUnionPoc.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MediatrUnionPoc.Infrastructure;

/// <summary>Wires up EF Core (InMemory, for this POC) and the repository/unit-of-work implementations.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers <see cref="AppDbContext"/> and its dependents as <c>Scoped</c> so a single HTTP
    /// request's repositories and <see cref="InMemoryUnitOfWork"/> share one tracked context —
    /// required for <see cref="InMemoryUnitOfWork"/>'s commit/rollback semantics to see the same
    /// staged changes the repositories made.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The same <paramref name="services"/> collection, for chaining.</returns>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase("MediatrUnionPoc")
        );
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IUnitOfWork, InMemoryUnitOfWork>();

        return services;
    }
}
