using System.Security.Claims;
using MediatrUnionPoc.Application.Common.Authorization;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Features.Products.Common;
using MediatrUnionPoc.Application.Features.Products.Update;
using MediatrUnionPoc.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace MediatrUnionPoc.Application.Tests.Handlers;

/// <summary>
/// Tests <see cref="UpdateProductHandler"/> directly against a substituted
/// <see cref="IProductRepository"/>, covering the Success, NotFound, and NotAuthorized union
/// cases — the latter exercised through a real <see cref="ResourceAuthorizationService"/> backed
/// by the same <see cref="OwnerAuthorizationHandler{TResource}"/> DI wiring registers, the same
/// style <c>OwnerAuthorizationHandlerTests</c> already uses for the underlying mechanism.
/// </summary>
public sealed class UpdateProductHandlerTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly ResourceAuthorizationService _resourceAuthorizationService;

    /// <summary>Wires up a real <see cref="IAuthorizationService"/> with the same policy/handler this feature registers in production.</summary>
    public UpdateProductHandlerTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore(options =>
            options.AddPolicy(
                AuthorizationPolicies.ProductOwner,
                policy =>
                    policy.Requirements.Add(
                        new OperationAuthorizationRequirement { Name = "Update" }
                    )
            )
        );
        services.AddSingleton<
            IAuthorizationHandler,
            OwnerAuthorizationHandler<OwnedProductResource>
        >();
        _provider = services.BuildServiceProvider();
        _resourceAuthorizationService = new ResourceAuthorizationService(
            _provider.GetRequiredService<IAuthorizationService>()
        );
    }

    /// <inheritdoc/>
    public void Dispose() => _provider.Dispose();

    /// <summary>Verifies an existing product owned by the caller is updated and the handler returns Success.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Updates_the_product_and_returns_Success_when_found_and_owned_by_the_caller()
    {
        var repository = Substitute.For<IProductRepository>();
        var product = Product.Create("Widget", Money.From(9.99m), ownerId: "owner-1");
        repository.GetByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);
        var handler = new UpdateProductHandler(repository, _resourceAuthorizationService);

        var result = await handler.Handle(
            new UpdateProductCommand(
                product.Id.Value,
                "Widget Pro",
                19.99m,
                PrincipalWithId("owner-1")
            ),
            CancellationToken.None
        );

        Assert.IsType<Success>(((System.Runtime.CompilerServices.IUnion)result).Value);
        Assert.Equal("Widget Pro", product.Name);
        Assert.Equal(19.99m, product.Price.Value);
    }

    /// <summary>Verifies a caller who doesn't own the product gets NotAuthorized and nothing is updated.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Returns_NotAuthorized_and_leaves_nothing_to_update_when_the_caller_does_not_own_the_product()
    {
        var repository = Substitute.For<IProductRepository>();
        var product = Product.Create("Widget", Money.From(9.99m), ownerId: "owner-1");
        repository.GetByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);
        var handler = new UpdateProductHandler(repository, _resourceAuthorizationService);

        var result = await handler.Handle(
            new UpdateProductCommand(
                product.Id.Value,
                "Widget Pro",
                19.99m,
                PrincipalWithId("owner-2")
            ),
            CancellationToken.None
        );

        Assert.IsType<NotAuthorized>(((System.Runtime.CompilerServices.IUnion)result).Value);
        Assert.Equal("Widget", product.Name);
        Assert.Equal(9.99m, product.Price.Value);
    }

    /// <summary>Verifies a missing product returns NotFound and nothing is updated, before any ownership check runs.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Returns_NotFound_and_leaves_nothing_to_update_when_missing()
    {
        var repository = Substitute.For<IProductRepository>();
        var missingId = Guid.NewGuid();

        // A concrete ProductId, not Arg.Any<ProductId>(): NSubstitute can't disambiguate two
        // Arg.Any<T>() matchers in the same call when T is a struct with Vogen's value equality.
        repository
            .GetByIdAsync(ProductId.From(missingId), Arg.Any<CancellationToken>())
            .Returns((Product?)null);
        var handler = new UpdateProductHandler(repository, _resourceAuthorizationService);

        var result = await handler.Handle(
            new UpdateProductCommand(missingId, "Name", 1m, PrincipalWithId("owner-1")),
            CancellationToken.None
        );

        var notFound = Assert.IsType<NotFound<ProductId>>(
            ((System.Runtime.CompilerServices.IUnion)result).Value
        );
        Assert.Equal(ProductId.From(missingId), notFound.Id);
    }

    private static ClaimsPrincipal PrincipalWithId(string id)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, id)],
            authenticationType: "Test"
        );
        return new ClaimsPrincipal(identity);
    }
}
