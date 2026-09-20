using FluentValidation;

namespace MediatrUnionPoc.Application.Features.Products.GetPaged;

/// <summary>
/// Validates <see cref="GetPagedProductsQuery"/> before <see cref="GetPagedProductsHandler"/> runs.
/// Every failure names the property it belongs to, so a caller can attribute each one to the
/// query-string parameter that caused it.
/// </summary>
public sealed class GetPagedProductsValidator : AbstractValidator<GetPagedProductsQuery>
{
    private const int MaxTextLength = 200;

    /// <summary>
    /// Enforces the paging bounds (a 1-based page number, a page size capped at 100 to bound how
    /// many rows one request can pull back), the length limits on the text filters (matching the
    /// limits on the stored name and owner), non-negative price bounds with the minimum not above
    /// the maximum, and a <see cref="GetPagedProductsQuery.Sort"/> that
    /// <see cref="ProductSortParser"/> accepts — one failure per problem it finds.
    /// </summary>
    public GetPagedProductsValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.NameContains).MaximumLength(MaxTextLength);
        RuleFor(x => x.OwnerId).MaximumLength(MaxTextLength);
        RuleFor(x => x.MinPrice).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.MaxPrice).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.MaxPrice)
            .Must((query, max) => max >= query.MinPrice)
            .When(x => x is { MinPrice: not null, MaxPrice: not null })
            .WithMessage("MaxPrice must not be less than MinPrice.");
        RuleFor(x => x.Sort)
            .Custom(
                (sort, context) =>
                {
                    if (!ProductSortParser.TryParse(sort, out _, out var problems))
                    {
                        foreach (var problem in problems)
                        {
                            context.AddFailure(problem);
                        }
                    }
                }
            );
    }
}
