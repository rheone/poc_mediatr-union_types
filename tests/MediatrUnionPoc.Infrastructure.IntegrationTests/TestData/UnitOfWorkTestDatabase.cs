using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Infrastructure.IntegrationTests.TestData;

/// <summary>The EF Core providers <see cref="EfCoreUnitOfWork"/> is exercised against.</summary>
public enum UnitOfWorkProvider
{
    /// <summary>The InMemory provider: no transaction support.</summary>
    InMemory,

    /// <summary>A SQLite <c>:memory:</c> connection: a relational provider with real transactions.</summary>
    Sqlite,
}

/// <summary>
/// One database, on either provider, that hands out contexts over the production
/// <see cref="AppDbContext"/> model so a test body can run identically against both.
/// </summary>
public sealed class UnitOfWorkTestDatabase : IAsyncDisposable
{
    private readonly string _inMemoryName;
    private readonly SqliteDatabaseMother? _sqlite;

    private UnitOfWorkTestDatabase(string inMemoryName, SqliteDatabaseMother? sqlite)
    {
        _inMemoryName = inMemoryName;
        _sqlite = sqlite;
    }

    /// <summary>Gets the SQLite database; only valid when created for <see cref="UnitOfWorkProvider.Sqlite"/>.</summary>
    public SqliteDatabaseMother Sqlite =>
        _sqlite ?? throw new InvalidOperationException("This database is not SQLite-backed.");

    /// <summary>Creates a fresh database on the given provider.</summary>
    /// <param name="provider">The provider to back it with.</param>
    /// <param name="name">A name unique to the calling test; only used by the InMemory provider.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous setup.</param>
    /// <returns>A task producing the ready-to-use database.</returns>
    public static async Task<UnitOfWorkTestDatabase> CreateAsync(
        UnitOfWorkProvider provider,
        string name,
        CancellationToken cancellationToken = default
    ) =>
        provider switch
        {
            UnitOfWorkProvider.InMemory => new UnitOfWorkTestDatabase(name, null),
            UnitOfWorkProvider.Sqlite => new UnitOfWorkTestDatabase(
                name,
                await SqliteDatabaseMother.CreateAsync(cancellationToken)
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(provider)),
        };

    /// <summary>Creates a context over this database; the caller disposes it.</summary>
    /// <returns>A new <see cref="AppDbContext"/>.</returns>
    public AppDbContext CreateContext() =>
        _sqlite is null ? DbContextMother.Create(_inMemoryName) : _sqlite.CreateContext();

    /// <summary>Saves the given products into the database through a throwaway context.</summary>
    /// <param name="products">The products to persist.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous seeding.</param>
    /// <returns>A task representing the asynchronous seeding.</returns>
    public async Task SeedAsync(
        IReadOnlyCollection<Product> products,
        CancellationToken cancellationToken = default
    )
    {
        await using var seedContext = CreateContext();
        await seedContext.Products.AddRangeAsync(products, cancellationToken);
        await seedContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _sqlite?.DisposeAsync() ?? ValueTask.CompletedTask;
}
