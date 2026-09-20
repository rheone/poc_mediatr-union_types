using Microsoft.Data.Sqlite;

namespace MediatrUnionPoc.Infrastructure;

/// <summary>
/// Owns the connection string <see cref="AppDbContext"/> uses and, when the database is an in-memory
/// SQLite one, the single open connection that keeps it alive. An in-memory SQLite database exists
/// only while at least one connection to it is open, and each request's context opens and closes its
/// own connection, so this singleton holds one open for the lifetime of the host.
/// </summary>
internal sealed class ProductsDatabase : IDisposable
{
    private readonly SqliteConnection? _keepAlive;

    /// <summary>Initializes a new instance of the <see cref="ProductsDatabase"/> class.</summary>
    /// <param name="connectionString">
    /// The configured connection string, or <see langword="null"/> for a private in-memory database
    /// that lives as long as this instance.
    /// </param>
    public ProductsDatabase(string? connectionString)
    {
        ConnectionString =
            connectionString ?? $"Data Source=products-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

        var builder = new SqliteConnectionStringBuilder(ConnectionString);
        if (builder.Mode == SqliteOpenMode.Memory || builder.DataSource == ":memory:")
        {
            _keepAlive = new SqliteConnection(ConnectionString);
            _keepAlive.Open();
        }
    }

    /// <summary>Gets the connection string every <see cref="AppDbContext"/> connects with.</summary>
    public string ConnectionString { get; }

    /// <inheritdoc/>
    public void Dispose() => _keepAlive?.Dispose();
}
