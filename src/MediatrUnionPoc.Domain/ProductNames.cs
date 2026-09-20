namespace MediatrUnionPoc.Domain;

/// <summary>
/// The single definition of when two product names are "the same": they match case-insensitively
/// after trimming surrounding whitespace. Both the application's up-front duplicate check and the
/// persistence layer's unique index derive their key from <see cref="Normalize"/>, so they cannot
/// disagree.
/// </summary>
public static class ProductNames
{
    /// <summary>Produces the canonical comparison key for a product name.</summary>
    /// <param name="name">The name as supplied by the caller.</param>
    /// <returns>The trimmed, upper-cased (invariant culture) form; two names are duplicates exactly when their keys are equal.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public static string Normalize(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.Trim().ToUpperInvariant();
    }
}
