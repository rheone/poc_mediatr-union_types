using MediatrUnionPoc.Application.Common.Results;

namespace MediatrUnionPoc.Api.Http;

/// <summary>
/// Shared policy the built-in union-to-HTTP extension members in <see cref="ResultHttpExtensions"/>
/// consult (through <see cref="HttpContext.RequestServices"/>) when they turn a union case into a
/// problem response. Registered with
/// <see cref="ResultHttpMappingServiceCollectionExtensions.AddResultHttpMapping"/> and configured
/// through the standard options pattern, so it can also be changed with
/// <c>services.Configure&lt;HttpMappingOptions&gt;(...)</c>. Anything an extension member does not
/// take from here can still be overridden per call, or bypassed entirely by writing a custom
/// <c>switch</c> arm.
/// </summary>
public sealed class HttpMappingOptions
{
    /// <summary>
    /// Maps an <see cref="Error.Code"/> to the HTTP status an <see cref="Error"/> carrying that code
    /// produces. Codes are compared ordinally. Ships with <see cref="Error.ValidationFailureCode"/>
    /// mapped to 400; add or replace entries to give other codes their own status.
    /// </summary>
    public IDictionary<string, int> ErrorStatusCodes { get; } =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [Error.ValidationFailureCode] = StatusCodes.Status400BadRequest,
        };

    /// <summary>Gets or sets the status used for an <see cref="Error"/> whose code is absent from <see cref="ErrorStatusCodes"/>. Defaults to 500.</summary>
    public int DefaultErrorStatusCode { get; set; } = StatusCodes.Status500InternalServerError;

    /// <summary>
    /// Gets or sets whether problem responses carry an RFC 7807 <c>type</c> URI (an entry of
    /// <see cref="TypeUris"/>). Defaults to <see langword="true"/>; when <see langword="false"/>
    /// the <c>type</c> member is omitted.
    /// </summary>
    public bool IncludeTypeUris { get; set; } = true;

    /// <summary>Status-to-<c>type</c>-URI table used when <see cref="IncludeTypeUris"/> is on; ships with the RFC references for 400, 401, 403, 404, 409, 412, 428 and 500. Add entries for other statuses, or replace these.</summary>
    public IDictionary<int, string> TypeUris { get; } =
        new Dictionary<int, string>
        {
            [StatusCodes.Status400BadRequest] = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            [StatusCodes.Status401Unauthorized] = "https://tools.ietf.org/html/rfc7235#section-3.1",
            [StatusCodes.Status403Forbidden] = "https://tools.ietf.org/html/rfc7231#section-6.5.3",
            [StatusCodes.Status404NotFound] = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            [StatusCodes.Status409Conflict] = "https://tools.ietf.org/html/rfc7231#section-6.5.8",
            [StatusCodes.Status412PreconditionFailed] =
                "https://tools.ietf.org/html/rfc7232#section-4.2",
            [StatusCodes.Status428PreconditionRequired] =
                "https://tools.ietf.org/html/rfc6585#section-3",
            [StatusCodes.Status500InternalServerError] =
                "https://tools.ietf.org/html/rfc7231#section-6.6.1",
        };

    /// <summary>Resolves the HTTP status for <paramref name="error"/> from <see cref="ErrorStatusCodes"/>, falling back to <see cref="DefaultErrorStatusCode"/>.</summary>
    /// <param name="error">The error to map.</param>
    /// <returns>The HTTP status code.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    public int StatusCodeFor(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return error.Code is not null && ErrorStatusCodes.TryGetValue(error.Code, out var status)
            ? status
            : DefaultErrorStatusCode;
    }

    /// <summary>Resolves the <c>type</c> URI for <paramref name="statusCode"/>, or <see langword="null"/> when <see cref="IncludeTypeUris"/> is off or no entry exists.</summary>
    /// <param name="statusCode">The response's HTTP status.</param>
    /// <returns>The URI string, or <see langword="null"/>.</returns>
    public string? TypeUriFor(int statusCode) =>
        IncludeTypeUris && TypeUris.TryGetValue(statusCode, out var uri) ? uri : null;
}
