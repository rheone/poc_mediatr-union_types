using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Application.Features.Products.GetPaged;

/// <summary>
/// Parses the textual sort specification a caller supplies (<c>name,-price</c>) into an ordered list of
/// <see cref="ProductSort"/> keys. Only <see cref="ProductSortField"/> members are accepted, so a
/// caller can never sort by anything outside the allowlist.
/// </summary>
public static class ProductSortParser
{
    // Built from the enum by name (never Enum.TryParse, which would also accept "1" or "name,price"),
    // camel-cased to match the wire names.
    private static readonly Dictionary<string, ProductSortField> FieldsByName =
        Enum.GetValues<ProductSortField>()
            .ToDictionary(
                field => char.ToLowerInvariant(field.ToString()[0]) + field.ToString()[1..],
                field => field,
                StringComparer.OrdinalIgnoreCase
            );

    private static readonly string AllowedFields = string.Join(", ", FieldsByName.Keys);

    /// <summary>Attempts to parse <paramref name="text"/>.</summary>
    /// <param name="text">
    /// Comma-separated keys, in priority order. Each key is a field name (<c>name</c>, <c>price</c>,
    /// <c>createdAt</c>; case-insensitive) optionally prefixed with <c>-</c> for descending. Whitespace
    /// around keys is ignored. <see langword="null"/> or blank text means "no sort requested".
    /// </param>
    /// <param name="sort">The parsed keys in priority order; empty when nothing was requested or when parsing failed.</param>
    /// <param name="errors">One message per problem found; empty when parsing succeeded.</param>
    /// <returns><see langword="true"/> when <paramref name="text"/> was valid.</returns>
    public static bool TryParse(
        string? text,
        out IReadOnlyList<ProductSort> sort,
        out IReadOnlyList<string> errors
    )
    {
        var keys = new List<ProductSort>();
        var problems = new List<string>();

        if (!string.IsNullOrWhiteSpace(text))
        {
            foreach (var token in text.Split(',', StringSplitOptions.TrimEntries))
            {
                if (token.Length == 0)
                {
                    problems.Add("The sort list contains an empty key.");
                    continue;
                }

                var descending = token.StartsWith('-');
                var name = descending ? token[1..].TrimStart() : token;

                if (!FieldsByName.TryGetValue(name, out var field))
                {
                    problems.Add(
                        $"'{(name.Length > 0 ? name : token)}' is not a sortable field; use one of: {AllowedFields}."
                    );
                }
                else if (keys.Exists(k => k.Field == field))
                {
                    problems.Add($"'{name}' is listed more than once in the sort.");
                }
                else
                {
                    keys.Add(
                        new ProductSort(
                            field,
                            descending ? SortDirection.Descending : SortDirection.Ascending
                        )
                    );
                }
            }
        }

        sort = problems.Count == 0 ? keys : [];
        errors = problems;
        return problems.Count == 0;
    }
}
