using FluentValidation;

namespace MediatrUnionPoc.Application.Features.Products.GetPaged;

/// <summary>Validates <see cref="GetPagedProductsQuery"/> before <see cref="GetPagedProductsHandler"/> runs.</summary>
public sealed class GetPagedProductsValidator : AbstractValidator<GetPagedProductsQuery>
{
    /// <summary>
    /// Enforces the paging bounds <see cref="GetPagedProductsQuery.PageNumber"/> and
    /// <see cref="GetPagedProductsQuery.PageSize"/> must satisfy: a 1-based page number, and a
    /// page size capped at 100 to bound how many rows a single request can pull back.
    /// </summary>
    public GetPagedProductsValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
