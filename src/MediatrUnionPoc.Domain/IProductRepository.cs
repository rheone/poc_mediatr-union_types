namespace MediatrUnionPoc.Domain;

/// <summary>
/// Persistence operations for <see cref="Product"/>, scoped to whatever unit of work
/// (<see cref="IUnitOfWork"/>) the current handler was given. Mutating members
/// (<see cref="AddAsync"/>, <see cref="Remove"/>) only stage a change; nothing reaches the
/// database until the unit of work commits, and a rollback discards it.
/// </summary>
public interface IProductRepository
{
    /// <summary>Looks up a single product by its identity.</summary>
    /// <param name="id">The product's identity.</param>
    /// <param name="cancellationToken">Token to cancel the lookup; defaults to <see cref="CancellationToken.None"/>.</param>
    /// <returns>The matching <see cref="Product"/>, or <see langword="null"/> if none exists.</returns>
    Task<Product?> GetByIdAsync(ProductId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves one page of the products matching <paramref name="criteria"/>, in the requested order,
    /// along with how many products match in total. Ordering always ends in an implicit
    /// <see cref="Product.Id"/> tiebreaker, so rows that tie on every requested key still page in a
    /// stable order and no row is skipped or repeated between pages.
    /// </summary>
    /// <param name="pageNumber">1-based page number.</param>
    /// <param name="pageSize">Maximum number of items per page.</param>
    /// <param name="criteria">Which products to include; <see cref="ProductCriteria.None"/> keeps all of them.</param>
    /// <param name="sort">The sort keys in priority order; empty means <see cref="ProductSort.Default"/>.</param>
    /// <param name="cancellationToken">Token to cancel the query; defaults to <see cref="CancellationToken.None"/>.</param>
    /// <returns>The requested page alongside the number of matching products (not the size of the table) and the sort that was applied.</returns>
    Task<PagedResult<Product>> GetPagedAsync(
        int pageNumber,
        int pageSize,
        ProductCriteria criteria,
        IReadOnlyList<ProductSort> sort,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Reports whether any stored product's name is a duplicate of <paramref name="name"/> under
    /// <see cref="ProductNames.Normalize"/> (case-insensitive, ignoring surrounding whitespace).
    /// Reads committed state only — products staged but not yet committed are invisible to it, which
    /// is why persistence also carries a unique index as the race backstop.
    /// </summary>
    /// <param name="name">The candidate name.</param>
    /// <param name="excludingId">A product to leave out of the comparison — the one being renamed, so it never collides with itself; <see langword="null"/> to compare against every product.</param>
    /// <param name="cancellationToken">Token to cancel the query; defaults to <see cref="CancellationToken.None"/>.</param>
    /// <returns><see langword="true"/> if another product already holds an equivalent name.</returns>
    Task<bool> ExistsWithNameAsync(
        string name,
        ProductId? excludingId = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>Stages a new product for insertion. Not persisted until the unit of work commits.</summary>
    /// <param name="product">The product to add.</param>
    /// <param name="cancellationToken">Token to cancel the operation; defaults to <see cref="CancellationToken.None"/>.</param>
    /// <returns>A task that completes once the product is staged.</returns>
    Task AddAsync(Product product, CancellationToken cancellationToken = default);

    /// <summary>Stages an existing, tracked product for deletion. Not persisted until the unit of work commits.</summary>
    /// <param name="product">The product to remove — must already be tracked (e.g. returned by <see cref="GetByIdAsync"/>).</param>
    void Remove(Product product);
}
