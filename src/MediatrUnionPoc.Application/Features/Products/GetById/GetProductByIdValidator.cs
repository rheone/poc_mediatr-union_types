using FluentValidation;

namespace MediatrUnionPoc.Application.Features.Products.GetById;

/// <summary>Validates <see cref="GetProductByIdQuery"/> before <see cref="GetProductByIdHandler"/> runs.</summary>
public sealed class GetProductByIdValidator : AbstractValidator<GetProductByIdQuery>
{
    /// <summary>Initializes the validation rules.</summary>
    public GetProductByIdValidator()
    {
        RuleFor(x => x.Id).NotEqual(Guid.Empty);
    }
}
