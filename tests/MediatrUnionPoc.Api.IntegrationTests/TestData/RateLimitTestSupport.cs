using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MediatrUnionPoc.Api.IntegrationTests.TestData;

/// <summary>
/// Shared arrangement for the rate-limiting tests. A limit is always tiny and its window always an hour, so
/// a test counts requests and never waits: the window cannot roll over while the test runs.
/// </summary>
public static class RateLimitTestSupport
{
    /// <summary>The window, in seconds, every limited policy gets (long enough that it never rolls over during a test).</summary>
    public const string LongWindowSeconds = "3600";

    /// <summary>The header carrying the seconds a refused caller must wait.</summary>
    public const string RetryAfterHeaderName = "Retry-After";

    /// <summary>The <c>code</c> member every rate-limit problem carries.</summary>
    public const string RateLimitedCode = "RATE_LIMITED";

    /// <summary>
    /// Derives a host whose named policies have the given per-window limits (an hour long); a policy left
    /// <see langword="null"/> keeps the factory's generous default.
    /// </summary>
    /// <param name="factory">The host to derive from.</param>
    /// <param name="reads">The <c>Reads</c> limit.</param>
    /// <param name="writes">The <c>Writes</c> limit.</param>
    /// <param name="impersonation">The <c>Impersonation</c> limit.</param>
    /// <param name="configure">Any further host configuration.</param>
    /// <returns>The derived host; the caller disposes it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <see langword="null"/>.</exception>
    public static WebApplicationFactory<Program> WithLimits(
        this WebApplicationFactory<Program> factory,
        int? reads = null,
        int? writes = null,
        int? impersonation = null,
        Action<IWebHostBuilder>? configure = null
    )
    {
        ArgumentNullException.ThrowIfNull(factory);

        return factory.WithWebHostBuilder(builder =>
        {
            Limit(builder, "Reads", reads);
            Limit(builder, "Writes", writes);
            Limit(builder, "Impersonation", impersonation);
            configure?.Invoke(builder);
        });
    }

    /// <summary>Sends one request, optionally from a chosen network address and with a browser <c>Origin</c>.</summary>
    /// <param name="client">The client to send with.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="uri">The request URI.</param>
    /// <param name="remoteAddress">The address the request appears to come from; the factory's default when <see langword="null"/>.</param>
    /// <param name="origin">The <c>Origin</c> header, if any.</param>
    /// <param name="content">A JSON body, if any.</param>
    /// <returns>The response.</returns>
    public static async Task<HttpResponseMessage> RequestAsync(
        this HttpClient client,
        HttpMethod method,
        string uri,
        string? remoteAddress = null,
        string? origin = null,
        object? content = null
    )
    {
        ArgumentNullException.ThrowIfNull(client);

        using var request = new HttpRequestMessage(method, uri);
        if (remoteAddress is not null)
        {
            request.Headers.TryAddWithoutValidation(
                ProductsApiFactory.RemoteAddressHeaderName,
                remoteAddress
            );
        }

        if (origin is not null)
        {
            request.Headers.TryAddWithoutValidation("Origin", origin);
        }

        if (content is not null)
        {
            request.Content = JsonContent.Create(content);
        }

        return await client.SendAsync(request, CancellationToken.None);
    }

    /// <summary>Sends <paramref name="count"/> identical <c>GET</c>s and returns the status code of each, in order.</summary>
    /// <param name="client">The client to send with.</param>
    /// <param name="uri">The request URI.</param>
    /// <param name="count">How many requests to send.</param>
    /// <param name="remoteAddress">The address the requests appear to come from.</param>
    /// <returns>The status codes.</returns>
    public static async Task<IReadOnlyList<int>> GetStatusesAsync(
        this HttpClient client,
        string uri,
        int count,
        string? remoteAddress = null
    )
    {
        List<int> statuses = [];
        for (var i = 0; i < count; i++)
        {
            using var response = await client.RequestAsync(HttpMethod.Get, uri, remoteAddress);
            statuses.Add((int)response.StatusCode);
        }

        return statuses;
    }

    /// <summary>Reads a response body as a JSON object.</summary>
    /// <param name="response">The response.</param>
    /// <returns>The body.</returns>
    public static async Task<JsonObject> ReadJsonAsync(this HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return (await response.Content.ReadFromJsonAsync<JsonObject>(CancellationToken.None))!;
    }

    /// <summary>Reads a response's <c>Retry-After</c> header as whole seconds, failing the test when it is absent or not a plain non-negative integer.</summary>
    /// <param name="response">The response.</param>
    /// <returns>The seconds.</returns>
    public static int RetryAfterSeconds(this HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);

        var text = response.Headers.GetValues(RetryAfterHeaderName).Single();
        Assert.True(
            int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds),
            $"Retry-After '{text}' is not whole seconds."
        );

        return seconds;
    }

    private static void Limit(IWebHostBuilder builder, string policy, int? limit)
    {
        if (limit is null)
        {
            return;
        }

        builder.UseSetting(
            $"RateLimiting:{policy}:PermitLimit",
            limit.Value.ToString(CultureInfo.InvariantCulture)
        );
        builder.UseSetting($"RateLimiting:{policy}:WindowSeconds", LongWindowSeconds);
    }
}
