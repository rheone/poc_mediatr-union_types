using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using MediatrUnionPoc.Api.Contracts;
using MediatrUnionPoc.Api.Impersonation;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MediatrUnionPoc.Api.IntegrationTests.TestData;

/// <summary>Shared arrangement for the impersonation tests: who mints, what they ask for, and how the answer is read.</summary>
public static class ImpersonationTestSupport
{
    /// <summary>The route of the token endpoint.</summary>
    public const string TokensUri = "/api/impersonation/tokens";

    /// <summary>A reason that satisfies the validator.</summary>
    public const string ValidReason = "Reproducing the checkout error alice reported";

    /// <summary>The id of an administrator caller.</summary>
    public const string AdminId = "root";

    /// <summary>The id of a support caller.</summary>
    public const string SupportId = "sam";

    /// <summary>The id of an ordinary user (also the usual impersonation target).</summary>
    public const string UserId = "alice";

    /// <summary>Creates a client presenting a real signed token for <paramref name="userId"/> holding <paramref name="roles"/>.</summary>
    /// <param name="factory">The host (real JWT mode).</param>
    /// <param name="userId">The token's subject.</param>
    /// <param name="roles">The token's roles.</param>
    /// <returns>A client with the bearer token set.</returns>
    public static HttpClient ClientAs(
        ProductsApiFactory factory,
        string userId,
        params string[] roles
    ) => ClientWithToken(factory, JwtTestTokens.Create(factory, userId, roles));

    /// <summary>Creates a client presenting <paramref name="token"/> as its bearer credential.</summary>
    /// <param name="factory">The host (possibly one derived with <c>WithWebHostBuilder</c>).</param>
    /// <param name="token">The compact token.</param>
    /// <returns>A client with the bearer token set.</returns>
    public static HttpClient ClientWithToken(WebApplicationFactory<Program> factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Builds a valid request body, overridable per test.</summary>
    /// <param name="target">The identity to act as.</param>
    /// <param name="roles">The roles to grant.</param>
    /// <param name="reason">The reason.</param>
    /// <param name="lifetimeMinutes">The requested lifetime, if any.</param>
    /// <param name="ticket">The ticket reference, if any.</param>
    /// <returns>The request body.</returns>
    public static IssueImpersonationTokenRequest Body(
        string? target = UserId,
        string[]? roles = null,
        string? reason = ValidReason,
        int? lifetimeMinutes = null,
        string? ticket = null
    ) => new(target, roles, reason, ticket, lifetimeMinutes);

    /// <summary>Posts <paramref name="body"/> to the token endpoint.</summary>
    /// <param name="client">The calling client.</param>
    /// <param name="body">The request body.</param>
    /// <returns>The response.</returns>
    public static Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        IssueImpersonationTokenRequest body
    ) => client.PostAsJsonAsync(TokensUri, body, CancellationToken.None);

    /// <summary>Mints a token as <paramref name="client"/> and returns the token string, failing the test if it is refused.</summary>
    /// <param name="client">The (privileged) calling client.</param>
    /// <param name="body">The request body.</param>
    /// <returns>The compact token.</returns>
    public static async Task<string> MintAsync(
        HttpClient client,
        IssueImpersonationTokenRequest body
    )
    {
        using var response = await PostAsync(client, body);
        response.EnsureSuccessStatusCode();
        var json = (await response.Content.ReadFromJsonAsync<JsonObject>(CancellationToken.None))!;
        return json["token"]!.GetValue<string>();
    }

    /// <summary>Gets the impersonation signing key the host is configured with.</summary>
    /// <param name="factory">The host.</param>
    /// <returns>The key.</returns>
    public static string ImpersonationKey(ProductsApiFactory factory) =>
        factory.Services.GetRequiredService<IOptions<ImpersonationOptions>>().Value.SigningKey;
}
