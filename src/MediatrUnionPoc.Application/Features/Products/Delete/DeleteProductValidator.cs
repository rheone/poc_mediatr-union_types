using FluentValidation;

namespace MediatrUnionPoc.Application.Features.Products.Delete;

/// <summary>Validates <see cref="DeleteProductCommand"/> before <see cref="DeleteProductHandler"/> runs.</summary>
public sealed class DeleteProductValidator : AbstractValidator<DeleteProductCommand>
{
    /// <summary>Initializes the validation rules.</summary>
    public DeleteProductValidator()
    {
        RuleFor(x => x.Id).NotEqual(Guid.Empty);
    }
}
