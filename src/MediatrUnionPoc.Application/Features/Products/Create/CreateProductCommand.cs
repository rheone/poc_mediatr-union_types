using System.Security.Claims;
using MediatrUnionPoc.Application.Common.Abstractions;

namespace MediatrUnionPoc.Application.Features.Products.Create;

/// <summary>Creates a new product. Validated by <see cref="CreateProductValidator"/> before <see cref="CreateProductHandler"/> ever runs.</summary>
/// <param name="Name">The product's display name. Must be non-empty, at most 200 characters.</param>
/// <param name="Price">The product's price. Must be zero or greater.</param>
/// <param name="Principal">
/// The caller's identity, used only by <see cref="CreateProductHandler"/> to assign the new
/// product's owner (its <see cref="ClaimTypes.NameIdentifier"/> claim, if any) — unlike
/// <see cref="Delete.DeleteProductCommand.Principal"/>, nothing authorizes against this; creating
/// a product has no ownership prerequisite. Defaults to <see langword="null"/> (an unowned
/// product) so every existing caller of this command keeps compiling unchanged.
/// </param>
public sealed record CreateProductCommand(
    string Name,
    decimal Price,
    ClaimsPrincipal? Principal = null
) : ITransactionalCommand<CreateProductResult>;
