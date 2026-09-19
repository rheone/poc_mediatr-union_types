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

    /// <summary>Verifies <see cref="DependencyInjection.AddInfrastructure"/> returns the collection it was given.</summary>
    // Auto Generated, verify expected behavior: AddInfrastructure returns the same collection for chaining.
    [Fact]
    public void AddInfrastructure_ServiceCollection_ReturnsSameCollection_Test()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddInfrastructure();

        // Assert
        Assert.Same(services, result);
    }

    /// <summary>Verifies the repository and unit-of-work abstractions resolve to their Infrastructure implementations.</summary>
    // Auto Generated, verify expected behavior: the two abstractions map to the Infrastructure implementations.
    [Fact]
    public void AddInfrastructure_Scope_ResolvesInfrastructureImplementations_Test()
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

    /// <summary>Verifies each service is scoped: same instance within a scope, a new one in the next.</summary>
    // Auto Generated, verify expected behavior: services are scoped, not singleton or transient.
    [Fact]
    public void AddInfrastructure_TwoScopes_ResolvesOneRepositoryPerScope_Test()
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

    /// <summary>Verifies a product staged by the scoped repository is persisted by the scoped unit of work.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    // Auto Generated, verify expected behavior: repository and unit of work share one context per scope.
    [Fact]
    public async Task CommitAsync_StagedThroughScopedRepository_PersistsForNewScope_Test()
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

    // SWEEP-AMBIGUITY: AddInfrastructure has no null guard on its `services` parameter, so a null
    // collection fails inside AddDbContext with a NullReferenceException. It should arguably throw
    // ArgumentNullException("services") up front; no test pins either behavior.
}
