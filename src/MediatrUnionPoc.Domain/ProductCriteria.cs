namespace MediatrUnionPoc.Domain;

/// <summary>
/// Which products a listing should include. Database-agnostic: it names conditions, and only
/// persistence decides how to evaluate them. Every member is optional; unset members impose no
/// restriction, and set members combine with AND.
/// </summary>
/// <param name="NameContains">
/// Keep products whose name contains this text, compared the way duplicate names are (see
/// <see cref="ProductNames.Normalize"/>): case-insensitively and ignoring surrounding whitespace.
/// <see langword="null"/> or whitespace-only text imposes no restriction.
/// </param>
/// <param name="MinPrice">Keep products priced at least this much (inclusive).</param>
/// <param name="MaxPrice">Keep products priced at most this much (inclusive).</param>
/// <param name="OwnerId">Keep products owned by exactly this caller identifier (case-sensitive).</param>
public sealed record ProductCriteria(
    string? NameContains = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    string? OwnerId = null
)
{
    /// <summary>The criteria that keep every product.</summary>
    public static ProductCriteria None { get; } = new();
}
