using Vogen;

namespace MediatrUnionPoc.Domain;

/// <summary>
/// The optimistic-concurrency version of a <see cref="Product"/>: a positive <see cref="long"/>
/// that starts at 1 for a new product and is advanced by the domain (never by the database) on
/// every mutation. Two writers that both loaded version <c>n</c> cannot both win — whoever writes
/// second finds the stored version is no longer <c>n</c>.
/// </summary>
[ValueObject<long>(conversions: Conversions.SystemTextJson)]
public readonly partial struct ProductVersion
{
    /// <summary>The version every newly created product starts at.</summary>
    public static ProductVersion Initial => From(1);

    /// <summary>Invoked by every Vogen-generated factory method; rejects zero and negative versions.</summary>
    private static Validation Validate(long input) =>
        input >= 1 ? Validation.Ok : Validation.Invalid("ProductVersion must be at least 1.");

    /// <summary>The version that follows this one.</summary>
    /// <returns>A <see cref="ProductVersion"/> exactly one greater than this.</returns>
    public ProductVersion Next() => From(Value + 1);

    /// <summary>Renders this version as a weak entity tag, <c>W/"n"</c> — the form used on the wire.</summary>
    /// <returns>The weak ETag string, quotes included.</returns>
    public string ToETag() => $"W/\"{Value}\"";

    /// <summary>Parses the weak entity tag form produced by <see cref="ToETag"/>.</summary>
    /// <param name="etag">The header value to parse, e.g. <c>W/"7"</c>.</param>
    /// <returns>The parsed version, or <see langword="null"/> if <paramref name="etag"/> is not a well-formed weak ETag of a valid version.</returns>
    public static ProductVersion? ParseETag(string? etag) =>
        etag is { Length: > 4 } // W/"" plus at least one digit
        && etag.StartsWith("W/\"", StringComparison.Ordinal)
        && etag.EndsWith('"')
        && long.TryParse(
            etag.AsSpan(3, etag.Length - 4),
            System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value
        )
        && TryFrom(value, out var version)
            ? version
            : null;
}
