using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MediatrUnionPoc.Infrastructure.IntegrationTests.TestData;

/// <summary>
/// A private SQLite <c>:memory:</c> database whose lifetime is the lifetime of one kept-open
/// <see cref="SqliteConnection"/> — the only test-side way to give <see cref="AppDbContext"/> a
/// relational provider (and therefore a real <c>IDbContextTransaction</c>) while keeping the
/// production model, value converters included. Every context created here shares the connection,
/// so they all see the same database.
/// </summary>
public sealed class SqliteDatabaseMother : IAsyncDisposable
{
    private SqliteDatabaseMother(SqliteConnection connection) => Connection = connection;

    /// <summary>Gets the kept-open connection, exposed so a test can break it deliberately.</summary>
    public SqliteConnection Connection { get; }

    /// <summary>Opens a fresh in-memory database and creates the production schema in it.</summary>
    /// <param name="cancellationToken">Token to cancel the asynchronous setup.</param>
    /// <returns>A task producing the ready-to-use database.</returns>
    public static async Task<SqliteDatabaseMother> CreateAsync(
        CancellationToken cancellationToken = default
    )
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(cancellationToken);
        var database = new SqliteDatabaseMother(connection);
        await using var context = database.CreateContext();
        await context.Database.EnsureCreatedAsync(cancellationToken);
        return database;
    }

    /// <summary>Creates a context over the shared connection, using the same model as production.</summary>
    /// <returns>A new <see cref="AppDbContext"/>; the caller disposes it (the connection is not disposed with it).</returns>
    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(Connection).Options;
        return new AppDbContext(options);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => Connection.DisposeAsync();
}
