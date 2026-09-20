using System.ComponentModel.DataAnnotations;

namespace MediatrUnionPoc.Api.Cors;

/// <summary>
/// Validates a list of HTTP method or header names: every entry a non-empty RFC 9110 token, never the
/// wildcard <c>*</c> (the lists are explicit), no duplicates ignoring case. A <see langword="null"/> list
/// is valid (the default applies); an empty list is valid only with <see cref="AllowEmpty"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class CorsTokenListAttribute : ValidationAttribute
{
    private const string TokenSymbols = "!#$%&'+-.^_`|~";

    /// <summary>Gets or sets a value indicating whether an empty (but not <see langword="null"/>) list is valid. Defaults to <see langword="false"/>.</summary>
    public bool AllowEmpty { get; set; }

    /// <inheritdoc/>
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        if (value is not string[] tokens)
        {
            return ValidationResult.Success;
        }

        if (tokens.Length == 0 && !AllowEmpty)
        {
            return Fail(validationContext, "must contain at least one entry.");
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in tokens)
        {
            if (string.IsNullOrEmpty(token) || token == "*" || !token.All(IsTokenCharacter))
            {
                return Fail(
                    validationContext,
                    $"'{token}' is not a valid token (non-empty, no spaces or separators, no wildcard)."
                );
            }

            if (!seen.Add(token))
            {
                return Fail(validationContext, $"'{token}' is listed more than once.");
            }
        }

        return ValidationResult.Success;
    }

    private static bool IsTokenCharacter(char c) =>
        char.IsAsciiLetterOrDigit(c) || TokenSymbols.Contains(c, StringComparison.Ordinal);

    private static ValidationResult Fail(ValidationContext context, string message) =>
        new($"{context.DisplayName}: {message}", [context.MemberName ?? context.DisplayName]);
}
