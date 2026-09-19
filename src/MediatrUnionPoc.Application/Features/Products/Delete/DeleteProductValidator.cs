using FluentValidation;

namespace MediatrUnionPoc.Application.Features.Products.Delete;

/// <summary>Validates <see cref="DeleteProductCommand"/> before <see cref="DeleteProductHandler"/> runs.</summary>
public sealed class DeleteProductValidator : AbstractValidator<DeleteProductCommand>
{
    /// <summary>
    /// Rejects <see cref="Guid.Empty"/> as an id — the only input shape this command has to validate
    /// before <see cref="DeleteProductHandler"/> attempts the lookup.
    /// </summary>
    public DeleteProductValidator()
    {
        RuleFor(x => x.Id).NotEqual(Guid.Empty);
    }
}
