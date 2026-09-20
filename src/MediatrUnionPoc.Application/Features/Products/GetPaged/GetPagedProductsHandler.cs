using MediatR;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Features.Products.Common;
using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Application.Features.Products.GetPaged;

/// <summary>
/// Lists the products matching the query's filters, in the requested order, a page at a time.
/// Translates the query's plain values into the Domain's <see cref="ProductCriteria"/> and
/// <see cref="ProductSort"/> keys and hands them to the repository; how they are evaluated is
/// persistence's business. Past validation (<see cref="GetPagedProductsValidator"/>) this handler
/// succeeds: there is no entity to be "not found" for a list, so an out-of-range page simply yields a
/// <see cref="PagedResult{T}"/> with an empty <see cref="PagedResult{T}.Items"/> collection.
/// </summary>
/// <param name="repository">The repository pages are read from.</param>
/// <exception cref="ArgumentNullException"><paramref name="repository"/> is <see langword="null"/>.</exception>
public sealed class GetPagedProductsHandler(IProductRepository repository)
    : IRequestHandler<GetPagedProductsQuery, GetPagedProductsResult>
{
    private readonly IProductRepository _repository =
        repository ?? throw new ArgumentNullException(nameof(repository));

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    public async Task<GetPagedProductsResult> Handle(
        GetPagedProductsQuery request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        // The validation behavior has already rejected a bad sort; parsing here again keeps the
        // handler correct when it is called directly, without an exception for expected input.
        if (!ProductSortParser.TryParse(request.Sort, out var sort, out var problems))
        {
            return new ValidationErrors(
                problems.Select(problem => new ValidationError(nameof(request.Sort), problem))
            );
        }

        var criteria = new ProductCriteria(
            request.NameContains,
            request.MinPrice,
            request.MaxPrice,
            request.OwnerId
        );

        var page = await _repository.GetPagedAsync(
            request.PageNumber,
            request.PageSize,
            criteria,
            sort,
            cancellationToken
        );

        return new PagedResult<ProductDto>(
            page.Items.Select(ProductDto.FromDomain).ToList(),
            page.PageNumber,
            page.PageSize,
            page.TotalCount,
            page.Sort
        );
    }
}
