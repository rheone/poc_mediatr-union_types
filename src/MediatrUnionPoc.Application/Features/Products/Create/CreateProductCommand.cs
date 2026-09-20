using System.Security.Claims;
using MediatrUnionPoc.Application.Common.Abstractions;
using MediatrUnionPoc.Application.Common.Auditing;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Features.Products.Common;

namespace MediatrUnionPoc.Application.Features.Products.Create;

/// <summary>Creates a new product. Validated by <see cref="CreateProductValidator"/> before <see cref="CreateProductHandler"/> ever runs.</summary>
/// <param name="Name">The product's display name. Must be non-empty, at most 200 characters.</param>
/// <remarks>
/// <see cref="Name"/> is deliberately not null-guarded in the constructor, unlike other public
/// reference-type parameters in this layer: <see cref="CreateProductValidator"/> owns that rule,
/// so a <see langword="null"/> name constructed by a direct <c>ISender.Send</c> caller comes back
/// as a <c>ValidationErrors</c> outcome rather than a thrown <see cref="ArgumentNullException"/>.
/// Over HTTP a <see langword="null"/> name never gets this far, since MVC model validation
/// rejects it (400) on the request DTO first, so the guard would be redundant there, not harmful.
/// </remarks>
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
) : ITransactionalCommand<CreateProductResult>, IAuditableRequest<CreateProductResult>
{
    /// <inheritdoc/>
    public string AuditAction => "Product.Create";

    /// <inheritdoc/>
    public AuditFailurePolicy AuditFailurePolicy => AuditFailurePolicy.BestEffort;

    /// <inheritdoc/>
    public ClaimsPrincipal? AuditPrincipal => Principal;

    /// <inheritdoc/>
    /// <remarks>The target is the id of the product the successful response carries; every other case created nothing, so it has none.</remarks>
    public AuditDescription DescribeAudit(CreateProductResult response) =>
        response switch
        {
            ProductDto product => ProductAudit.Target(product.Id.Value.ToString()),
            ValidationErrors or Error or Conflict => ProductAudit.Target(null),
        };
}
