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
    /// <remarks>Results are untracked (read-only path); ordered by name so paging is deterministic.</remarks>
    public async Task<PagedResult<Product>> GetPagedAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default
    )
    {
        var totalCount = await _dbContext.Products.CountAsync(cancellationToken);

        var items = await _dbContext
            .Products.AsNoTracking()
            .OrderBy(p => p.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Product>(items, pageNumber, pageSize, totalCount);
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
