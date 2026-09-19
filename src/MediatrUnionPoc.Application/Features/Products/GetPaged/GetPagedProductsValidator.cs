using FluentValidation;

namespace MediatrUnionPoc.Application.Features.Products.GetPaged;

/// <summary>Validates <see cref="GetPagedProductsQuery"/> before <see cref="GetPagedProductsHandler"/> runs.</summary>
public sealed class GetPagedProductsValidator : AbstractValidator<GetPagedProductsQuery>
{
    /// <summary>Initializes the validation rules.</summary>
    public GetPagedProductsValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
