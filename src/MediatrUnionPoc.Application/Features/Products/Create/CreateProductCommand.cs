using MediatrUnionPoc.Application.Common.Abstractions;

namespace MediatrUnionPoc.Application.Features.Products.Create;

/// <summary>Creates a new product. Validated by <see cref="CreateProductValidator"/> before <see cref="CreateProductHandler"/> ever runs.</summary>
/// <param name="Name">The product's display name. Must be non-empty, at most 200 characters.</param>
/// <param name="Price">The product's price. Must be zero or greater.</param>
public sealed record CreateProductCommand(string Name, decimal Price)
    : ITransactionalCommand<CreateProductResult>;
