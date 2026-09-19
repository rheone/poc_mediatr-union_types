using MediatrUnionPoc.Domain;
using Microsoft.EntityFrameworkCore;

namespace MediatrUnionPoc.Infrastructure;

/// <inheritdoc cref="IProductRepository"/>
public sealed class ProductRepository(AppDbContext dbContext) : IProductRepository
{
    /// <inheritdoc/>
    public Task<Product?> GetByIdAsync(
        ProductId id,
        CancellationToken cancellationToken = default
    ) => dbContext.Products.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    /// <inheritdoc/>
    public async Task<PagedResult<Product>> GetPagedAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default
    )
    {
        var totalCount = await dbContext.Products.CountAsync(cancellationToken);

        var items = await dbContext
            .Products.OrderBy(p => p.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Product>(items, pageNumber, pageSize, totalCount);
    }

    /// <inheritdoc/>
    public async Task AddAsync(Product product, CancellationToken cancellationToken = default) =>
        await dbContext.Products.AddAsync(product, cancellationToken);

    /// <inheritdoc/>
    public void Remove(Product product) => dbContext.Products.Remove(product);
}
