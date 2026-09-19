using System.Security.Claims;
using MediatrUnionPoc.Application.Features.Products.Common;
using MediatrUnionPoc.Application.Features.Products.Create;
using MediatrUnionPoc.Domain;
using NSubstitute;

namespace MediatrUnionPoc.Application.Tests.Handlers;

/// <summary>
/// Tests <see cref="CreateProductHandler"/> directly against a substituted
/// <see cref="IProductRepository"/>.
/// </summary>
public class CreateProductHandlerTests
{
    /// <summary>Verifies the handler adds the product to the repository and returns its DTO.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Adds_the_product_and_returns_its_dto()
    {
        var repository = Substitute.For<IProductRepository>();
        var handler = new CreateProductHandler(repository);

        var result = await handler.Handle(
            new CreateProductCommand("Widget", 9.99m),
            CancellationToken.None
        );

        var dto = Assert.IsType<ProductDto>(((System.Runtime.CompilerServices.IUnion)result).Value);
        Assert.Equal("Widget", dto.Name);
        Assert.Equal(9.99m, dto.Price);
        await repository
            .Received(1)
            .AddAsync(Arg.Is<Product>(p => p.Name == "Widget"), Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies a product created without a <see cref="CreateProductCommand.Principal"/> gets an empty owner id.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Assigns_an_empty_owner_id_when_no_principal_is_given()
    {
        var repository = Substitute.For<IProductRepository>();
        var handler = new CreateProductHandler(repository);

        await handler.Handle(new CreateProductCommand("Widget", 9.99m), CancellationToken.None);

        await repository
            .Received(1)
            .AddAsync(
                Arg.Is<Product>(p => p.OwnerId == string.Empty),
                Arg.Any<CancellationToken>()
            );
    }

    /// <summary>Verifies a product created with a principal carrying a name identifier claim is owned by that claim's value.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Assigns_the_principals_name_identifier_claim_as_the_owner_id()
    {
        var repository = Substitute.For<IProductRepository>();
        var handler = new CreateProductHandler(repository);
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "owner-1")],
            authenticationType: "Test"
        );
        var principal = new ClaimsPrincipal(identity);

        await handler.Handle(
            new CreateProductCommand("Widget", 9.99m, principal),
            CancellationToken.None
        );

        await repository
            .Received(1)
            .AddAsync(Arg.Is<Product>(p => p.OwnerId == "owner-1"), Arg.Any<CancellationToken>());
    }
}
