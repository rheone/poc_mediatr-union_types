using System.ComponentModel.DataAnnotations;

namespace MediatrUnionPoc.Api.Cors;

/// <summary>
/// Validates a list of CORS origins: every entry a canonical absolute <c>http</c>/<c>https</c> origin
/// (<c>scheme://host[:port]</c>, lowercase, default port omitted, no user info, path, query, fragment or
/// trailing slash, exactly the form a browser puts in the <c>Origin</c> header), no wildcard, no
/// duplicates. A <see langword="null"/> list is valid (nothing configured).
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class CorsOriginListAttribute : ValidationAttribute
{
    /// <inheritdoc/>
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        if (value is not string[] origins)
        {
            return ValidationResult.Success;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var origin in origins)
        {
            var problem = Describe(origin);
            if (problem is not null)
            {
                return Fail(validationContext, $"'{origin}' {problem}");
            }

            if (!seen.Add(origin))
            {
                return Fail(validationContext, $"'{origin}' is listed more than once.");
            }
        }

        return ValidationResult.Success;
    }

    private static string? Describe(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            return "is empty.";
        }

        if (origin.Contains('*', StringComparison.Ordinal))
        {
            return "is a wildcard; list each allowed origin explicitly.";
        }

        if (
            !Uri.TryCreate(origin, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        )
        {
            return "is not an absolute http or https origin.";
        }

        // The canonical form drops a default port, lowercases the host and has no path, query or
        // trailing slash, so a value that differs from it after round-tripping is not a plain origin.
        var isPlainOrigin =
            uri.UserInfo.Length == 0
            && string.Equals(
                origin,
                uri.GetLeftPart(UriPartial.Authority),
                StringComparison.Ordinal
            );

        return isPlainOrigin
            ? null
            : "must be scheme://host[:port], lowercase, with no user info, path, query, fragment or trailing slash.";
    }

    private static ValidationResult Fail(ValidationContext context, string message) =>
        new($"{context.DisplayName}: {message}", [context.MemberName ?? context.DisplayName]);
}
