using MediatrUnionPoc.Domain;
using Microsoft.EntityFrameworkCore;

namespace MediatrUnionPoc.Infrastructure;

/// <inheritdoc cref="IProductRepository"/>
/// <exception cref="ArgumentNullException"><paramref name="dbContext"/> is <see langword="null"/>.</exception>
public sealed class ProductRepository(AppDbContext dbContext) : IProductRepository
{
    private readonly AppDbContext _dbContext =
        dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    /// <inheritdoc/>
    public Task<Product?> GetByIdAsync(
        ProductId id,
        CancellationToken cancellationToken = default
    ) => _dbContext.Products.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    /// <inheritdoc/>
    /// <remarks>Results are untracked (read-only path).</remarks>
    public async Task<PagedResult<Product>> GetPagedAsync(
        int pageNumber,
        int pageSize,
        ProductCriteria criteria,
        IReadOnlyList<ProductSort> sort,
        CancellationToken cancellationToken = default
    )
    {
        var matching = ApplyCriteria(_dbContext.Products.AsNoTracking(), criteria);
        var totalCount = await matching.CountAsync(cancellationToken);

        var appliedSort = sort.Count == 0 ? ProductSort.Default : sort;

        var items = await ApplySort(matching, appliedSort)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Product>(items, pageNumber, pageSize, totalCount, appliedSort);
    }

    private static IOrderedQueryable<Product> ApplySort(
        IQueryable<Product> products,
        IReadOnlyList<ProductSort> sort
    )
    {
        IOrderedQueryable<Product>? ordered = null;

        foreach (var key in sort)
        {
            ordered = (ordered, key.Field, key.Direction) switch
            {
                (null, ProductSortField.Name, SortDirection.Ascending) => products.OrderBy(p =>
                    p.Name
                ),
                (null, ProductSortField.Name, SortDirection.Descending) =>
                    products.OrderByDescending(p => p.Name),
                (null, ProductSortField.Price, SortDirection.Ascending) => products.OrderBy(p =>
                    p.Price
                ),
                (null, ProductSortField.Price, SortDirection.Descending) =>
                    products.OrderByDescending(p => p.Price),
                (null, ProductSortField.CreatedAt, SortDirection.Ascending) => products.OrderBy(p =>
                    p.CreatedAt
                ),
                (null, ProductSortField.CreatedAt, SortDirection.Descending) =>
                    products.OrderByDescending(p => p.CreatedAt),
                (_, ProductSortField.Name, SortDirection.Ascending) => ordered.ThenBy(p => p.Name),
                (_, ProductSortField.Name, SortDirection.Descending) => ordered.ThenByDescending(
                    p => p.Name
                ),
                (_, ProductSortField.Price, SortDirection.Ascending) => ordered.ThenBy(p =>
                    p.Price
                ),
                (_, ProductSortField.Price, SortDirection.Descending) => ordered.ThenByDescending(
                    p => p.Price
                ),
                (_, ProductSortField.CreatedAt, SortDirection.Ascending) => ordered.ThenBy(p =>
                    p.CreatedAt
                ),
                (_, ProductSortField.CreatedAt, SortDirection.Descending) =>
                    ordered.ThenByDescending(p => p.CreatedAt),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(sort),
                    key,
                    "Unsupported sort key."
                ),
            };
        }

        // The implicit tiebreaker: whatever the caller asked for, rows that tie on every key still
        // have one total order, so consecutive pages never skip or repeat a row.
        return ordered is null ? products.OrderBy(p => p.Id) : ordered.ThenBy(p => p.Id);
    }

    private static IQueryable<Product> ApplyCriteria(
        IQueryable<Product> products,
        ProductCriteria criteria
    )
    {
        if (!string.IsNullOrWhiteSpace(criteria.NameContains))
        {
            // NormalizedName is already trimmed and upper-cased (ProductNames.Normalize), so
            // normalising the search text the same way gives a case-insensitive match that needs
            // no provider-specific collation or function.
            var key = ProductNames.Normalize(criteria.NameContains);
            products = products.Where(p => p.NormalizedName.Contains(key));
        }

        if (criteria.MinPrice is { } min)
        {
            var floor = Money.From(min);
            products = products.Where(p => p.Price >= floor);
        }

        if (criteria.MaxPrice is { } max)
        {
            var ceiling = Money.From(max);
            products = products.Where(p => p.Price <= ceiling);
        }

        if (criteria.OwnerId is { } ownerId)
        {
            products = products.Where(p => p.OwnerId == ownerId);
        }

        return products;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public Task<bool> ExistsWithNameAsync(
        string name,
        ProductId? excludingId = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(name);

        var key = ProductNames.Normalize(name);
        return _dbContext.Products.AnyAsync(
            p => p.NormalizedName == key && (excludingId == null || p.Id != excludingId.Value),
            cancellationToken
        );
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="product"/> is <see langword="null"/>.</exception>
    public async Task AddAsync(Product product, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(product);
        await _dbContext.Products.AddAsync(product, cancellationToken);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="product"/> is <see langword="null"/>.</exception>
    public void Remove(Product product)
    {
        ArgumentNullException.ThrowIfNull(product);
        _dbContext.Products.Remove(product);
    }
}
