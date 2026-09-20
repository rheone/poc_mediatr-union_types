using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MediatrUnionPoc.Api.Contracts;
using MediatrUnionPoc.Api.IntegrationTests.TestData;
using MediatrUnionPoc.Application.Features.Products.Common;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>
/// Exercises the real JWT bearer scheme (no test handler): tokens signed with the host's configured
/// key are accepted, expired, wrongly signed or wrongly addressed ones are refused with 401, and the
/// standard <c>sub</c> and <c>role</c> claims reach the Application layer as the
/// <c>NameIdentifier</c> and <c>Role</c> claims it authorizes against.
/// </summary>
[Trait("Category", "Integration")]
public sealed class JwtBearerAuthenticationTests : IDisposable
{
    private const string ProductsUri = "/api/products";
    private const string OtherSigningKey =
        "a-completely-different-signing-key-of-sufficient-length";

    private readonly ProductsApiFactory _factory = new(ApiAuthentication.RealJwt);

    /// <summary>Gets the tokens for <see cref="Get_UnacceptableToken_Returns401Problem_Test"/>: expired, signed with another key, and issued for another audience.</summary>
    public static TheoryData<string> Get_UnacceptableToken_Returns401Problem_Test_Data =>
        new() { "expired", "wrong-signature", "wrong-audience", "malformed" };

    /// <summary>Disposes the test's backing <see cref="ProductsApiFactory"/>.</summary>
    public void Dispose() => _factory.Dispose();

    /// <summary>Verifies a valid token reaches an endpoint (the list is empty, but answered 200).</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_ValidToken_Returns200_Test()
    {
        // Arrange
        using var client = ClientFor(JwtTestTokens.Create(_factory, "alice"));

        // Act
        using var response = await client.GetAsync(ProductsUri, CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Verifies an expired, wrongly signed, wrongly addressed or malformed token is refused with a 401 problem carrying a bearer challenge.</summary>
    /// <param name="kind">Which unacceptable token to present.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [MemberData(nameof(Get_UnacceptableToken_Returns401Problem_Test_Data))]
    public async Task Get_UnacceptableToken_Returns401Problem_Test(string kind)
    {
        // Arrange
        var token = kind switch
        {
            "expired" => JwtTestTokens.Create(_factory, "alice", lifetime: TimeSpan.FromHours(-1)),
            "wrong-signature" => JwtTestTokens.Create(
                _factory,
                "alice",
                signingKey: OtherSigningKey
            ),
            "wrong-audience" => JwtTestTokens.Create(_factory, "alice", audience: "someone-else"),
            _ => "not.a.jwt",
        };
        using var client = ClientFor(token);

        // Act
        using var response = await client.GetAsync(ProductsUri, CancellationToken.None);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode),
            () =>
                Assert.Equal(
                    "application/problem+json",
                    response.Content.Headers.ContentType?.MediaType
                ),
            () =>
                Assert.Contains(
                    response.Headers.WwwAuthenticate,
                    challenge => challenge.Scheme == "Bearer"
                ),
            () => Assert.NotEmpty(response.Headers.GetValues("X-Trace-Id"))
        );
    }

    /// <summary>Verifies no token at all is a 401 with a bearer challenge on the real scheme.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_NoToken_Returns401WithBearerChallenge_Test()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        using var response = await client.GetAsync(ProductsUri, CancellationToken.None);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode),
            () =>
                Assert.Contains(
                    response.Headers.WwwAuthenticate,
                    challenge => challenge.Scheme == "Bearer"
                )
        );
    }

    /// <summary>
    /// Verifies the standard <c>sub</c> claim becomes the product's owner through the real claim
    /// mapping: the token's subject can update what it created, another subject cannot.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Create_TokenSubject_BecomesOwner_Test()
    {
        // Arrange
        using var alice = ClientFor(JwtTestTokens.Create(_factory, "alice"));
        using var bob = ClientFor(JwtTestTokens.Create(_factory, "bob"));
        var created = await CreateAsync(alice);
        using var updateAsAlice = UpdateRequest(created);
        using var updateAsBob = UpdateRequest(created);

        // Act
        using var byOwner = await alice.SendAsync(updateAsAlice, CancellationToken.None);
        using var byOther = await bob.SendAsync(updateAsBob, CancellationToken.None);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.NoContent, byOwner.StatusCode),
            () => Assert.Equal(HttpStatusCode.Forbidden, byOther.StatusCode)
        );
    }

    /// <summary>Verifies a <c>role</c> claim of <c>Administrator</c> in a real token passes the delete authorization, and one without it is refused.</summary>
    /// <param name="roles">The role claim values in the token.</param>
    /// <param name="expected">The status the delete should return.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData(new[] { "Administrator" }, HttpStatusCode.NoContent)]
    [InlineData(new[] { "Support", "Administrator" }, HttpStatusCode.NoContent)]
    [InlineData(new[] { "Support" }, HttpStatusCode.Forbidden)]
    [InlineData(new string[0], HttpStatusCode.Forbidden)]
    public async Task Delete_TokenRoleClaim_MapsToAdministratorPolicy_Test(
        string[] roles,
        HttpStatusCode expected
    )
    {
        // Arrange
        using var owner = ClientFor(JwtTestTokens.Create(_factory, "alice"));
        var created = await CreateAsync(owner);
        using var caller = ClientFor(JwtTestTokens.Create(_factory, "carol", roles));

        // Act
        using var response = await caller.DeleteAsync(
            $"{ProductsUri}/{created.Id.Value}",
            CancellationToken.None
        );

        // Assert
        Assert.Equal(expected, response.StatusCode);
    }

    /// <summary>Verifies a validly signed token with no <c>sub</c> is authenticated but cannot create (403), and creates nothing.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Create_TokenWithoutSubject_Returns403_Test()
    {
        // Arrange
        using var client = ClientFor(JwtTestTokens.Create(_factory, subject: null));

        // Act
        using var response = await client.PostAsJsonAsync(
            ProductsUri,
            ProductRequestMother.Widget(),
            CancellationToken.None
        );

        // Assert
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode),
            () =>
                Assert.Equal(
                    "application/problem+json",
                    response.Content.Headers.ContentType?.MediaType
                )
        );
    }

    private static async Task<ProductDto> CreateAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            ProductsUri,
            ProductRequestMother.Widget(),
            CancellationToken.None
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductDto>(CancellationToken.None))!;
    }

    private static HttpRequestMessage UpdateRequest(ProductDto product)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"{ProductsUri}/{product.Id.Value}")
        {
            Content = JsonContent.Create(ProductRequestMother.WidgetPro()),
        };
        request.Headers.TryAddWithoutValidation("If-Match", product.Version.ToETag());
        return request;
    }

    private HttpClient ClientFor(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
