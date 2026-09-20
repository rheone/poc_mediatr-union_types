namespace MediatrUnionPoc.Domain;

/// <summary>One key of a multi-key sort: a <see cref="ProductSortField"/> and the <see cref="SortDirection"/> to order it in.</summary>
/// <param name="Field">The property to order by.</param>
/// <param name="Direction">Ascending or descending.</param>
public sealed record ProductSort(ProductSortField Field, SortDirection Direction)
{
    /// <summary>
    /// The order applied when a caller asks for none: <see cref="Product.Name"/> ascending. Every
    /// listing additionally ends in an implicit <see cref="Product.Id"/> tiebreaker (added by
    /// persistence, not represented here) so equal keys still page in a stable order.
    /// </summary>
    public static IReadOnlyList<ProductSort> Default { get; } =
    [new(ProductSortField.Name, SortDirection.Ascending)];
}
