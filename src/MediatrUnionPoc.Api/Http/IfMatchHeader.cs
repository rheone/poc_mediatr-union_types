using System.Diagnostics;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Api.Http;

/// <summary>The request carried no <c>If-Match</c> header (or an empty one).</summary>
[DebuggerDisplay("MissingIfMatch")]
public sealed record MissingIfMatch;

/// <summary>
/// What an <c>If-Match</c> request header amounts to for a product: the <see cref="ProductVersion"/>
/// it names, no header at all (<see cref="MissingIfMatch"/>), or a header that is not a well-formed
/// weak ETag (<see cref="ValidationErrors"/> naming the header). Each endpoint decides what a missing
/// header means — required on PUT (428), optional on DELETE.
/// </summary>
[DebuggerDisplay("{Value}")]
public union IfMatchHeader(ProductVersion, MissingIfMatch, ValidationErrors)
{
    /// <summary>The request header name the ETag is read from.</summary>
    public const string HeaderName = "If-Match";

    /// <summary>Classifies a raw <c>If-Match</c> header value.</summary>
    /// <param name="headerValue">The raw header value, or <see langword="null"/> when the header is absent.</param>
    /// <returns>The version it names, <see cref="MissingIfMatch"/> when absent or blank, or <see cref="ValidationErrors"/> when malformed.</returns>
    public static IfMatchHeader Parse(string? headerValue)
    {
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return new MissingIfMatch();
        }

        return ProductVersion.ParseETag(headerValue.Trim()) is { } version
            ? version
            : new ValidationErrors(
                [
                    new ValidationError(
                        HeaderName,
                        $"'{headerValue}' is not a valid entity tag; expected the form W/\"<version>\" returned in the ETag header."
                    ),
                ]
            );
    }
}
