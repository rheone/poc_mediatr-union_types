using FluentValidation;
using MediatrUnionPoc.Application.Features.Products.Common;

namespace MediatrUnionPoc.Application.Features.Products.Create;

/// <summary>Validates <see cref="CreateProductCommand"/> before <see cref="CreateProductHandler"/> runs.</summary>
public sealed class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    /// <summary>
    /// Enforces the constraints <see cref="CreateProductCommand.Name"/> and
    /// <see cref="CreateProductCommand.Price"/> document at the property level: the type system
    /// alone can't reject an empty or over-length name, or a negative price, so FluentValidation
    /// does it here before <see cref="CreateProductHandler"/> ever runs.
    /// </summary>
    public CreateProductValidator()
    {
        RuleFor(x => x.Name).MustBeValidProductName();
        RuleFor(x => x.Price).MustBeValidProductPrice();
    }
}
