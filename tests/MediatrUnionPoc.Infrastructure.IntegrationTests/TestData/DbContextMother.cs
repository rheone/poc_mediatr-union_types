using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;

namespace MediatrUnionPoc.Infrastructure.IntegrationTests.TestData;

/// <summary>Object mother for <see cref="AppDbContext"/> instances backed by the EF Core InMemory provider.</summary>
public static class DbContextMother
{
    /// <summary>
    /// Builds a database name unique to the calling test, so tests never share state and no random
    /// identifier is needed.
    /// </summary>
    /// <param name="testClass">The name of the calling test class.</param>
    /// <param name="test">The calling test method; supplied by the compiler.</param>
    /// <returns>A deterministic InMemory database name.</returns>
    public static string NameFor(string testClass, [CallerMemberName] string test = "") =>
        $"{testClass}.{test}";

    /// <summary>Creates a context over the named InMemory database; contexts sharing a name share data.</summary>
    /// <param name="databaseName">The InMemory database name.</param>
    /// <returns>A new <see cref="AppDbContext"/>.</returns>
    public static AppDbContext Create(string databaseName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>Saves the given products into the named database through a throwaway context.</summary>
    /// <param name="databaseName">The InMemory database name.</param>
    /// <param name="products">The products to persist.</param>
    /// <returns>A task representing the asynchronous seeding.</returns>
    public static async Task SeedAsync(string databaseName, params Domain.Product[] products)
    {
        await using var seedContext = Create(databaseName);
        await seedContext.Products.AddRangeAsync(products);
        await seedContext.SaveChangesAsync();
    }
}
