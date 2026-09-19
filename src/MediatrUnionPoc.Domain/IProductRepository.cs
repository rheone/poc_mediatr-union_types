namespace MediatrUnionPoc.Domain;

/// <summary>
/// Persistence operations for <see cref="Product"/>, scoped to whatever unit of work
/// (<see cref="IUnitOfWork"/>) the current handler was given. Mutating members
/// (<see cref="AddAsync"/>, <see cref="Remove"/>) only stage a change against the tracked
/// context; nothing reaches the database until the unit of work commits.
/// </summary>
public interface IProductRepository
{
    /// <summary>Looks up a single product by its identity.</summary>
    /// <param name="id">The product's identity.</param>
    /// <param name="cancellationToken">Token to cancel the lookup; defaults to <see cref="CancellationToken.None"/>.</param>
    /// <returns>The matching <see cref="Product"/>, or <see langword="null"/> if none exists.</returns>
    Task<Product?> GetByIdAsync(ProductId id, CancellationToken cancellationToken = default);

    /// <summary>Retrieves one page of products, ordered by name, along with the total row count.</summary>
    /// <param name="pageNumber">1-based page number.</param>
    /// <param name="pageSize">Maximum number of items per page.</param>
    /// <param name="cancellationToken">Token to cancel the query; defaults to <see cref="CancellationToken.None"/>.</param>
    /// <returns>The requested page of products alongside the total row count.</returns>
    Task<PagedResult<Product>> GetPagedAsync(
        int pageNumber,
        int pageSize,
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
