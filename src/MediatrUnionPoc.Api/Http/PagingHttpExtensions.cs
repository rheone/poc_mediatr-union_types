using System.Globalization;
using MediatrUnionPoc.Domain;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Net.Http.Headers;

namespace MediatrUnionPoc.Api.Http;

/// <summary>
/// C# 14 extension members that put a <see cref="PagedResult{T}"/>'s navigation on the wire as
/// headers: <c>X-Total-Count</c> and an RFC 8288 <c>Link</c> header, so a client can page without
/// parsing the body. The values come from the members of <see cref="PagedResult{T}"/>, which is the
/// one place the page arithmetic lives.
/// </summary>
public static class PagingHttpExtensions
{
    /// <summary>The response header carrying the number of items matching the request's filters, across all pages.</summary>
    public const string TotalCountHeaderName = "X-Total-Count";

    /// <summary>The query-string parameter that selects the page; the only one a <c>Link</c> URL changes.</summary>
    public const string PageNumberParameterName = "pageNumber";

    extension(HttpResponse response)
    {
        /// <summary>Sets <c>X-Total-Count</c> and the <c>Link</c> header describing <paramref name="page"/>, built from this response's request.</summary>
        /// <typeparam name="T">The type of item being paged.</typeparam>
        /// <param name="page">The page being returned.</param>
        /// <param name="canonicalUrl">The URL (path, optionally with the request's path base) the <c>Link</c> URLs are built on instead of the request's own path; used to point every client, including one that called an alias route, at the canonical versioned address. The request's query string is still used.</param>
        /// <exception cref="ArgumentNullException"><paramref name="response"/> or <paramref name="page"/> is <see langword="null"/>.</exception>
        public void SetPagingHeaders<T>(PagedResult<T> page, string? canonicalUrl = null)
        {
            ArgumentNullException.ThrowIfNull(response);
            ArgumentNullException.ThrowIfNull(page);

            response.Headers[TotalCountHeaderName] = page.TotalCount.ToString(
                CultureInfo.InvariantCulture
            );
            response.Headers[HeaderNames.Link] = page.ToLinkHeader(
                response.HttpContext.Request,
                canonicalUrl
            );
        }
    }

    extension<T>(PagedResult<T> page)
    {
        /// <summary>
        /// Builds the RFC 8288 <c>Link</c> header value for this page: <c>first</c>, <c>prev</c>,
        /// <c>next</c> and <c>last</c> relations, in that order, omitting <c>prev</c> when there is no
        /// previous page and <c>next</c> when there is no next one. Each URL is the request's own, with
        /// every query parameter kept in its original order and only <c>pageNumber</c> replaced (or
        /// appended if the request did not send it).
        /// </summary>
        /// <param name="request">The request the page answers.</param>
        /// <param name="canonicalUrl">When given, the URL (already including any path base) used instead of the request's own path.</param>
        /// <returns>The header value.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
        public string ToLinkHeader(HttpRequest request, string? canonicalUrl = null)
        {
            ArgumentNullException.ThrowIfNull(request);

            var links = new List<string> { LinkTo(request, canonicalUrl, page.FirstPage, "first") };

            if (page.PreviousPage is { } previous)
            {
                links.Add(LinkTo(request, canonicalUrl, previous, "prev"));
            }

            if (page.NextPage is { } next)
            {
                links.Add(LinkTo(request, canonicalUrl, next, "next"));
            }

            links.Add(LinkTo(request, canonicalUrl, page.LastPage, "last"));

            return string.Join(", ", links);
        }
    }

    private static string LinkTo(
        HttpRequest request,
        string? canonicalUrl,
        int pageNumber,
        string rel
    )
    {
        var pageValue = pageNumber.ToString(CultureInfo.InvariantCulture);
        var parameters = new List<KeyValuePair<string, string?>>();
        var replaced = false;

        foreach (var (name, values) in request.Query)
        {
            if (string.Equals(name, PageNumberParameterName, StringComparison.OrdinalIgnoreCase))
            {
                if (!replaced)
                {
                    parameters.Add(new(name, pageValue));
                    replaced = true;
                }

                continue;
            }

            parameters.AddRange(
                values.Select(value => new KeyValuePair<string, string?>(name, value))
            );
        }

        if (!replaced)
        {
            parameters.Add(new(PageNumberParameterName, pageValue));
        }

        var url = UriHelper.BuildAbsolute(
            request.Scheme,
            request.Host,
            canonicalUrl is null ? request.PathBase : PathString.Empty,
            canonicalUrl is null ? request.Path : new PathString(canonicalUrl),
            QueryString.Create(parameters)
        );

        return $"<{url}>; rel=\"{rel}\"";
    }
}
