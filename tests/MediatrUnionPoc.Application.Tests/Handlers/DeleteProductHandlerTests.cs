using System.Security.Claims;
using MediatrUnionPoc.Application.Common.Authorization;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Features.Products.Delete;
using MediatrUnionPoc.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace MediatrUnionPoc.Application.Tests.Handlers;

/// <summary>
/// Tests <see cref="DeleteProductHandler"/> directly against a substituted
/// <see cref="IProductRepository"/>, covering the Success, NotFound, and NotAuthorized union
/// cases — the latter exercised through a real <see cref="ResourceAuthorizationService"/> backed by
/// the same two-handler <c>ProductOwnerOrAdministrator</c> DI wiring registers
/// (<see cref="OwnerAuthorizationHandler{TResource}"/> and
/// <see cref="AdministratorResourceOverrideAuthorizationHandler{TResource}"/>), the same style
/// <c>UpdateProductHandlerTests</c> already uses for the single-handler <c>ProductOwner</c> policy.
/// </summary>
public sealed class DeleteProductHandlerTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly ResourceAuthorizationService _resourceAuthorizationService;

    /// <summary>Wires up a real <see cref="IAuthorizationService"/> with the same policy/handlers this feature registers in production.</summary>
    public DeleteProductHandlerTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore(options =>
            options.AddPolicy(
                AuthorizationPolicies.ProductOwnerOrAdministrator,
                policy =>
                    policy.Requirements.Add(
                        new OperationAuthorizationRequirement { Name = "Delete" }
                    )
            )
        );
        services.AddSingleton<
            IAuthorizationHandler,
            OwnerAuthorizationHandler<MediatrUnionPoc.Application.Features.Products.Common.OwnedProductResource>
        >();
        services.AddSingleton<IAuthorizationHandler>(
            _ => new AdministratorResourceOverrideAuthorizationHandler<MediatrUnionPoc.Application.Features.Products.Common.OwnedProductResource>(
                "Delete"
            )
        );
        _provider = services.BuildServiceProvider();
        _resourceAuthorizationService = new ResourceAuthorizationService(
            _provider.GetRequiredService<IAuthorizationService>()
        );
    }

    /// <inheritdoc/>
    public void Dispose() => _provider.Dispose();

    /// <summary>Verifies the product's owner can remove it and the handler returns Success.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Removes_the_product_and_returns_Success_when_the_caller_owns_it()
    {
        var repository = Substitute.For<IProductRepository>();
        var product = Product.Create("Widget", Money.From(9.99m), ownerId: "owner-1");
        repository.GetByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);
        var handler = new DeleteProductHandler(repository, _resourceAuthorizationService);

        var result = await handler.Handle(
            new DeleteProductCommand(product.Id.Value, PrincipalWithId("owner-1")),
            CancellationToken.None
        );

        Assert.IsType<Success>(((System.Runtime.CompilerServices.IUnion)result).Value);
        repository.Received(1).Remove(product);
    }

    /// <summary>Verifies an administrator can remove a product they don't own and the handler returns Success.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Removes_the_product_and_returns_Success_when_the_caller_is_an_administrator_but_not_the_owner()
    {
        var repository = Substitute.For<IProductRepository>();
        var product = Product.Create("Widget", Money.From(9.99m), ownerId: "owner-1");
        repository.GetByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);
        var handler = new DeleteProductHandler(repository, _resourceAuthorizationService);

        var result = await handler.Handle(
            new DeleteProductCommand(product.Id.Value, PrincipalWithRole("Administrator")),
            CancellationToken.None
        );

        Assert.IsType<Success>(((System.Runtime.CompilerServices.IUnion)result).Value);
        repository.Received(1).Remove(product);
    }

    /// <summary>Verifies a caller who neither owns the product nor is an administrator gets NotAuthorized and nothing is removed.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Returns_NotAuthorized_and_removes_nothing_when_the_caller_neither_owns_the_product_nor_is_an_administrator()
    {
        var repository = Substitute.For<IProductRepository>();
        var product = Product.Create("Widget", Money.From(9.99m), ownerId: "owner-1");
        repository.GetByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);
        var handler = new DeleteProductHandler(repository, _resourceAuthorizationService);

        var result = await handler.Handle(
            new DeleteProductCommand(product.Id.Value, PrincipalWithId("owner-2")),
            CancellationToken.None
        );

        Assert.IsType<NotAuthorized>(((System.Runtime.CompilerServices.IUnion)result).Value);
        repository.DidNotReceive().Remove(Arg.Any<Product>());
    }

    /// <summary>Verifies a missing product returns NotFound and nothing is removed, before any authorization check runs.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Returns_NotFound_and_removes_nothing_when_missing()
    {
        var repository = Substitute.For<IProductRepository>();
        var missingId = Guid.NewGuid();

        // A concrete ProductId, not Arg.Any<ProductId>(): NSubstitute can't disambiguate two
        // Arg.Any<T>() matchers in the same call when T is a struct with Vogen's value equality.
        repository
            .GetByIdAsync(ProductId.From(missingId), Arg.Any<CancellationToken>())
            .Returns((Product?)null);
        var handler = new DeleteProductHandler(repository, _resourceAuthorizationService);

        var result = await handler.Handle(
            new DeleteProductCommand(missingId, PrincipalWithId("owner-1")),
            CancellationToken.None
        );

        var notFound = Assert.IsType<NotFound<ProductId>>(
            ((System.Runtime.CompilerServices.IUnion)result).Value
        );
        Assert.Equal(ProductId.From(missingId), notFound.Id);
        repository.DidNotReceive().Remove(Arg.Any<Product>());
    }

    private static ClaimsPrincipal PrincipalWithId(string id)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, id)],
            authenticationType: "Test"
        );
        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal PrincipalWithRole(string role)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, role)],
            authenticationType: "Test"
        );
        return new ClaimsPrincipal(identity);
    }
}
