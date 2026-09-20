using MediatrUnionPoc.Domain;
using Microsoft.Net.Http.Headers;

namespace MediatrUnionPoc.Api.Http;

/// <summary>C# 14 extension members for writing a product's version to the wire as its weak <c>ETag</c>.</summary>
public static class ETagHttpExtensions
{
    extension(HttpResponse response)
    {
        /// <summary>Sets the <c>ETag</c> response header to the weak entity tag of <paramref name="version"/>.</summary>
        /// <param name="version">The product version the response describes.</param>
        /// <exception cref="ArgumentNullException"><paramref name="response"/> is <see langword="null"/>.</exception>
        public void SetETag(ProductVersion version)
        {
            ArgumentNullException.ThrowIfNull(response);

            response.Headers[HeaderNames.ETag] = version.ToETag();
        }
    }
}
