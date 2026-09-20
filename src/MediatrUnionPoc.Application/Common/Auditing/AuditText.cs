using System.Diagnostics.CodeAnalysis;

namespace MediatrUnionPoc.Application.Common.Auditing;

/// <summary>Bounds the free text that reaches an audit record, so unvalidated input (which is audited before validation has run) cannot bloat the file.</summary>
public static class AuditText
{
    /// <summary>The most characters any single text member of an event may have.</summary>
    public const int MaxLength = 512;

    private const string Ellipsis = "...";

    /// <summary>Truncates <paramref name="value"/> to <see cref="MaxLength"/> characters, marking the cut.</summary>
    /// <param name="value">The text, or <see langword="null"/>.</param>
    /// <returns>The bounded text.</returns>
    [return: NotNullIfNotNull(nameof(value))]
    public static string? Limit(string? value) =>
        value is null || value.Length <= MaxLength
            ? value
            : string.Concat(value.AsSpan(0, MaxLength - Ellipsis.Length), Ellipsis);
}
