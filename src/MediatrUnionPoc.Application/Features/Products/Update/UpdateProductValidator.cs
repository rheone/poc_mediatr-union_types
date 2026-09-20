using FluentValidation;
using MediatrUnionPoc.Application.Features.Products.Common;

namespace MediatrUnionPoc.Application.Features.Products.Update;

/// <summary>Validates <see cref="UpdateProductCommand"/> before <see cref="UpdateProductHandler"/> runs.</summary>
public sealed class UpdateProductValidator : AbstractValidator<UpdateProductCommand>
{
    /// <summary>
    /// Enforces the constraints <see cref="UpdateProductCommand.Id"/>, <see cref="UpdateProductCommand.Name"/>,
    /// and <see cref="UpdateProductCommand.Price"/> document at the property level: the type system
    /// alone can't reject a default <see cref="Guid"/>, an empty or over-length name, or a negative
    /// price, so FluentValidation does it here before <see cref="UpdateProductHandler"/> ever runs.
    /// A failure here short-circuits to
    /// <see cref="MediatrUnionPoc.Application.Common.Results.ValidationErrors"/> before the
    /// product is even loaded — earlier than the ownership check <see cref="UpdateProductHandler"/>
    /// runs itself, once loading has succeeded.
    /// </summary>
    public UpdateProductValidator()
    {
        RuleFor(x => x.Id).NotEqual(Guid.Empty);
        RuleFor(x => x.Name).MustBeValidProductName();
        RuleFor(x => x.Price).MustBeValidProductPrice();
    }
}
