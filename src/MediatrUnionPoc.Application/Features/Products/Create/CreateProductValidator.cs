using FluentValidation;

namespace MediatrUnionPoc.Application.Features.Products.Create;

/// <summary>Validates <see cref="CreateProductCommand"/> before <see cref="CreateProductHandler"/> runs.</summary>
public sealed class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    /// <summary>Initializes the validation rules.</summary>
    public CreateProductValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
    }
}
