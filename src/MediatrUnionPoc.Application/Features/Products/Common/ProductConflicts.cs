using MediatrUnionPoc.Application.Common.Results;

namespace MediatrUnionPoc.Application.Features.Products.Common;

/// <summary>Builds the <see cref="Conflict"/> cases products can produce, so every path reports a duplicate name the same way.</summary>
internal static class ProductConflicts
{
    /// <summary>Another product already holds an equivalent name (see <see cref="Domain.ProductNames"/>).</summary>
    /// <param name="name">The name the caller supplied; echoed back, and nothing about the product that holds it.</param>
    /// <returns>The conflict, whose message names the product-name field.</returns>
    public static Conflict NameTaken(string name) =>
        new(
            $"A product named '{name}' already exists. Product names must be unique, ignoring case and surrounding whitespace."
        );

    /// <summary>The database's unique index rejected a name a concurrent request claimed first.</summary>
    /// <returns>The conflict, whose message names the product-name field.</returns>
    public static Conflict NameTakenByConcurrentRequest() =>
        new(
            "A product with this name already exists. Product names must be unique, ignoring case and surrounding whitespace."
        );
}
