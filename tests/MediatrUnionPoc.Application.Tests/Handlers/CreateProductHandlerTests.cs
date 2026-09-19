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
}
