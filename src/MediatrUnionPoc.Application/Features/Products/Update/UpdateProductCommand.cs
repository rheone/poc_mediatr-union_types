using MediatrUnionPoc.Application.Common.Abstractions;

namespace MediatrUnionPoc.Application.Features.Products.Update;

/// <summary>Replaces a product's name and price in full — there is no partial-update support.</summary>
/// <param name="Id">The product's identity.</param>
/// <param name="Name">The product's new display name. Must be non-empty, at most 200 characters.</param>
/// <param name="Price">The product's new price. Must be zero or greater.</param>
public sealed record UpdateProductCommand(Guid Id, string Name, decimal Price)
    : ITransactionalCommand<UpdateProductResult>;
