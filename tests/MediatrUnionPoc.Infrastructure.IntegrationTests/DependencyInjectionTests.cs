using MediatrUnionPoc.Domain;
using MediatrUnionPoc.Infrastructure.IntegrationTests.TestData;
using Microsoft.Extensions.DependencyInjection;

namespace MediatrUnionPoc.Infrastructure.IntegrationTests;

/// <summary>
/// Verifies <see cref="DependencyInjection.AddInfrastructure"/> wires the repository and unit of
/// work so that, inside one scope, they share the same <see cref="AppDbContext"/>. That sharing is
/// what lets <see cref="InMemoryUnitOfWork.CommitAsync"/> persist what the repository staged.
/// </summary>
[Trait("Category", "Integration")]
public class DependencyInjectionTests
{
    private static ServiceProvider BuildProvider() =>
        new ServiceCollection()
            .AddInfrastructure()
            .BuildServiceProvider(
                new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }
            );

    // Auto Generated, verify expected behavior: AddInfrastructure returns the same collection for chaining.
    /// <summary>Verifies <see cref="DependencyInjection.AddInfrastructure"/> returns the collection it was given.</summary>
    [Fact]
    public void AddInfrastructure_given_a_service_collection_returns_the_same_collection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddInfrastructure();

        // Assert
        Assert.Same(services, result);
    }

    // Auto Generated, verify expected behavior: the two abstractions map to the Infrastructure implementations.
    /// <summary>Verifies the repository and unit-of-work abstractions resolve to their Infrastructure implementations.</summary>
    [Fact]
    public void AddInfrastructure_given_a_scope_resolves_the_infrastructure_implementations()
    {
        // Arrange
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        // Act
        var repository = scope.ServiceProvider.GetRequiredService<IProductRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        // Assert
        Assert.Multiple(
            () => Assert.IsType<ProductRepository>(repository),
            () => Assert.IsType<InMemoryUnitOfWork>(unitOfWork)
        );
    }

    // Auto Generated, verify expected behavior: services are scoped, not singleton or transient.
    /// <summary>Verifies each service is scoped: same instance within a scope, a new one in the next.</summary>
    [Fact]
    public void AddInfrastructure_given_two_scopes_resolves_one_repository_per_scope()
    {
        // Arrange
        using var provider = BuildProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        // Act
        var first = firstScope.ServiceProvider.GetRequiredService<IProductRepository>();
        var firstAgain = firstScope.ServiceProvider.GetRequiredService<IProductRepository>();
        var second = secondScope.ServiceProvider.GetRequiredService<IProductRepository>();

        // Assert
        Assert.Multiple(() => Assert.Same(first, firstAgain), () => Assert.NotSame(first, second));
    }

    // Auto Generated, verify expected behavior: repository and unit of work share one context per scope.
    /// <summary>Verifies a product staged by the scoped repository is persisted by the scoped unit of work.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task CommitAsync_after_staging_through_the_scoped_repository_persists_for_a_new_scope()
    {
        // Arrange
        using var provider = BuildProvider();
        var product = ProductMother.Widget();
        using (var writeScope = provider.CreateScope())
        {
            var repository = writeScope.ServiceProvider.GetRequiredService<IProductRepository>();
            var unitOfWork = writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await repository.AddAsync(product, CancellationToken.None);

            // Act
            await unitOfWork.CommitAsync(CancellationToken.None);
        }

        // Assert
        using var readScope = provider.CreateScope();
        var stored = await readScope
            .ServiceProvider.GetRequiredService<IProductRepository>()
            .GetByIdAsync(product.Id, CancellationToken.None);
        Assert.NotNull(stored);
    }
}
