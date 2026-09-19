using FluentValidation;

namespace MediatrUnionPoc.Application.Features.Products.Update;

/// <summary>Validates <see cref="UpdateProductCommand"/> before <see cref="UpdateProductHandler"/> runs.</summary>
public sealed class UpdateProductValidator : AbstractValidator<UpdateProductCommand>
{
    /// <summary>Initializes the validation rules.</summary>
    public UpdateProductValidator()
    {
        RuleFor(x => x.Id).NotEqual(Guid.Empty);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
    }
}
