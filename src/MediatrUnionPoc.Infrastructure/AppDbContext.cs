using MediatrUnionPoc.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MediatrUnionPoc.Infrastructure;

/// <summary>The only EF Core context in this POC — one aggregate, one table.</summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    /// <summary>The name of the unique index on <see cref="Product.NormalizedName"/>.</summary>
    public const string ProductNameIndexName = "UX_Products_NormalizedName";

    /// <summary>The products table.</summary>
    public DbSet<Product> Products => Set<Product>();

    /// <inheritdoc/>
    /// <remarks>
    /// <see cref="Product"/>'s Vogen value objects (<see cref="ProductId"/>, <see cref="Money"/>, <see cref="ProductVersion"/>)
    /// need explicit <see cref="ValueConverter{TModel,TProvider}"/> registration — EF Core has no
    /// built-in awareness of them, and Vogen's own generated converter isn't used here (see
    /// <see cref="ProductIdValueConverter"/>'s remarks on why).
    /// </remarks>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(builder =>
        {
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).HasConversion(new ProductIdValueConverter());
            builder.Property(p => p.Name).IsRequired().HasMaxLength(200);

            // The race backstop for the "product names are unique" rule. The rule's normalisation
            // (trim, case-insensitive) lives in the Domain (ProductNames.Normalize); the entity keeps
            // this column in step with Name, so a plain unique index on it enforces exactly the rule
            // on any relational provider, with no provider-specific collation.
            builder.Property(p => p.NormalizedName).IsRequired().HasMaxLength(200);
            builder
                .HasIndex(p => p.NormalizedName)
                .IsUnique()
                .HasDatabaseName(ProductNameIndexName);
            builder.Property(p => p.Price).HasConversion(new MoneyValueConverter());
            builder
                .Property(p => p.Version)
                .HasConversion(new ProductVersionValueConverter())
                .IsConcurrencyToken();
            builder.Property(p => p.OwnerId).IsRequired().HasMaxLength(200);
        });
    }
}
