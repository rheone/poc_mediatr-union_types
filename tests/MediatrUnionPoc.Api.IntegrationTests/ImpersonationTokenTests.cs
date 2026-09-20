using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using MediatrUnionPoc.Api.IntegrationTests.TestData;
using MediatrUnionPoc.Application.Common.Authorization;
using MediatrUnionPoc.Application.Features.Products.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using static MediatrUnionPoc.Api.IntegrationTests.TestData.ImpersonationTestSupport;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>
/// Exercises what an issued impersonation token is and how the real JWT bearer scheme treats it: the
/// claims it carries (and how they read back on the principal), authentication as the target, the two
/// accepted signing keys, expiry, and the refusal of chained impersonation.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ImpersonationTokenTests : IDisposable
{
    private const string ProductsUri = ApiRoutes.Products;
    private const string RandomKey = "a-completely-different-signing-key-of-sufficient-length";

    private readonly ProductsApiFactory _factory = new(ApiAuthentication.RealJwt);

    /// <summary>Disposes the test's backing <see cref="ProductsApiFactory"/>.</summary>
    public void Dispose() => _factory.Dispose();

    /// <summary>
    /// Verifies the token, validated with the host's own bearer handler and parameters, yields a principal
    /// whose <c>NameIdentifier</c> is the target, roles are the granted ones, and which carries the marker,
    /// reason, ticket and an <c>act</c> claim the helpers read as the real caller.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Token_ValidatedByTheBearerHandler_CarriesTargetRolesActorMarkerAndReason_Test()
    {
        // Arrange
        using var minter = ClientAs(_factory, AdminId, "Administrator");
        var token = await MintAsync(
            minter,
            Body(roles: ["Support", "Administrator"], ticket: "SUP-7")
        );
        var bearer = _factory
            .Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        var handler = bearer.TokenHandlers.OfType<JsonWebTokenHandler>().Single();

        // Act
        var result = await handler.ValidateTokenAsync(token, bearer.TokenValidationParameters);
        var principal = new ClaimsPrincipal(result.ClaimsIdentity);

        // Assert
        Assert.Multiple(
            () => Assert.True(result.IsValid),
            () => Assert.Equal(UserId, principal.FindFirst(ClaimTypes.NameIdentifier)?.Value),
            () => Assert.True(principal.IsInRole("Support")),
            () => Assert.True(principal.IsInRole("Administrator")),
            () => Assert.True(principal.IsImpersonated()),
            () => Assert.Equal(AdminId, principal.GetActorId()),
            () =>
                Assert.Equal("true", principal.FindFirst(ImpersonationClaims.Impersonated)?.Value),
            () => Assert.Equal(ValidReason, principal.FindFirst(ImpersonationClaims.Reason)?.Value),
            () => Assert.Equal("SUP-7", principal.FindFirst(ImpersonationClaims.Ticket)?.Value),
            () => Assert.NotNull(principal.FindFirst("jti")),
            () => Assert.NotNull(principal.FindFirst("iat"))
        );
    }

    /// <summary>Verifies the raw token holds <c>act</c> as a nested JSON object naming the real caller, next to <c>sub</c>, <c>iat</c>, <c>nbf</c>, <c>exp</c> and <c>jti</c>, and is HS256 signed.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Token_Payload_HasActAsJsonObjectAndTimeClaims_Test()
    {
        // Arrange
        using var minter = ClientAs(_factory, SupportId, "Support");
        var token = await MintAsync(minter, Body(lifetimeMinutes: 10));

        // Act
        var jwt = new JsonWebToken(token);

        // Assert
        var actor = jwt.GetPayloadValue<JsonElement>("act");
        Assert.Multiple(
            () => Assert.Equal(JsonValueKind.Object, actor.ValueKind),
            () => Assert.Equal(SupportId, actor.GetProperty("sub").GetString()),
            () => Assert.Equal(UserId, jwt.Subject),
            () => Assert.Equal("HS256", jwt.Alg),
            () => Assert.True(jwt.GetPayloadValue<bool>("impersonated")),
            () => Assert.NotEmpty(jwt.Id),
            () => Assert.Equal(TimeSpan.FromMinutes(10), jwt.ValidTo - jwt.ValidFrom),
            () => Assert.Equal(jwt.ValidFrom, jwt.IssuedAt)
        );
    }

    /// <summary>Verifies a minted token authenticates as the target: what it creates is owned by the target, who can then update it.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task MintedToken_Creates_ProductOwnedByTheTarget_Test()
    {
        // Arrange
        using var minter = ClientAs(_factory, SupportId, "Support");
        var token = await MintAsync(minter, Body());
        using var asTarget = ClientWithToken(_factory, token);
        using var alice = ClientAs(_factory, UserId);
        using var bob = ClientAs(_factory, "bob");

        // Act
        using var created = await asTarget.PostAsJsonAsync(
            ProductsUri,
            ProductRequestMother.Widget(),
            CancellationToken.None
        );
        var product = (
            await created.Content.ReadFromJsonAsync<ProductDto>(CancellationToken.None)
        )!;
        using var updateAsTarget = UpdateRequest(product);
        using var updateAsOther = UpdateRequest(product);
        using var byTarget = await alice.SendAsync(updateAsTarget, CancellationToken.None);
        using var byOther = await bob.SendAsync(updateAsOther, CancellationToken.None);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.Created, created.StatusCode),
            () => Assert.Equal(HttpStatusCode.NoContent, byTarget.StatusCode),
            () => Assert.Equal(HttpStatusCode.Forbidden, byOther.StatusCode)
        );
    }

    /// <summary>Verifies a minted token's roles are honoured on protected endpoints: Administrator can delete, no roles cannot.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task MintedToken_Roles_DriveProtectedEndpoints_Test()
    {
        // Arrange
        using var admin = ClientAs(_factory, AdminId, "Administrator");
        using var owner = ClientAs(_factory, UserId);
        using var created = await owner.PostAsJsonAsync(
            ProductsUri,
            ProductRequestMother.Widget(),
            CancellationToken.None
        );
        var product = (
            await created.Content.ReadFromJsonAsync<ProductDto>(CancellationToken.None)
        )!;
        using var withoutRole = ClientWithToken(
            _factory,
            await MintAsync(admin, Body(target: "bob"))
        );
        using var withRole = ClientWithToken(
            _factory,
            await MintAsync(admin, Body(target: "carol", roles: ["Administrator"]))
        );

        // Act
        using var refused = await withoutRole.DeleteAsync(
            $"{ProductsUri}/{product.Id.Value}",
            CancellationToken.None
        );
        using var allowed = await withRole.DeleteAsync(
            $"{ProductsUri}/{product.Id.Value}",
            CancellationToken.None
        );

        // Assert
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode),
            () => Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode)
        );
    }

    /// <summary>Verifies a token signed with the impersonation key is accepted by the bearer scheme, and one signed with an unknown key is refused.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_TokenSignedWithImpersonationKeyVersusRandomKey_AcceptedVersusRefused_Test()
    {
        // Arrange
        using var withImpersonationKey = ClientWithToken(
            _factory,
            JwtTestTokens.Create(_factory, UserId, signingKey: ImpersonationKey(_factory))
        );
        using var withRandomKey = ClientWithToken(
            _factory,
            JwtTestTokens.Create(_factory, UserId, signingKey: RandomKey)
        );

        // Act
        using var accepted = await withImpersonationKey.GetAsync(
            ProductsUri,
            CancellationToken.None
        );
        using var refused = await withRandomKey.GetAsync(ProductsUri, CancellationToken.None);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.OK, accepted.StatusCode),
            () => Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode)
        );
    }

    /// <summary>Verifies an expired impersonation token, both crafted and actually minted on an earlier clock, is refused.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_ExpiredImpersonationToken_Returns401_Test()
    {
        // Arrange
        using var pastClock = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<TimeProvider>(
                    new ManualTimeProvider(DateTimeOffset.UtcNow.AddHours(-3))
                )
            )
        );
        using var minter = ClientWithToken(
            pastClock,
            JwtTestTokens.Create(_factory, AdminId, ["Administrator"])
        );
        var minted = await MintAsync(minter, Body(lifetimeMinutes: 10));
        using var crafted = ClientWithToken(
            _factory,
            JwtTestTokens.Create(
                _factory,
                UserId,
                lifetime: TimeSpan.FromHours(-1),
                signingKey: ImpersonationKey(_factory)
            )
        );
        using var mintedClient = ClientWithToken(_factory, minted);

        // Act
        using var fromCrafted = await crafted.GetAsync(ProductsUri, CancellationToken.None);
        using var fromMinted = await mintedClient.GetAsync(ProductsUri, CancellationToken.None);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.Unauthorized, fromCrafted.StatusCode),
            () => Assert.Equal(HttpStatusCode.Unauthorized, fromMinted.StatusCode)
        );
    }

    /// <summary>Verifies an impersonation token that carries a qualifying role still cannot mint another: chained impersonation is a 403 that says so.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Post_CallerUsingAnImpersonationToken_Returns403ChainedImpersonation_Test()
    {
        // Arrange
        using var admin = ClientAs(_factory, AdminId, "Administrator");
        var token = await MintAsync(admin, Body(target: "bob", roles: ["Administrator"]));
        using var chained = ClientWithToken(_factory, token);

        // Act
        using var response = await PostAsync(chained, Body(target: "carol"));

        // Assert
        using var problem = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(CancellationToken.None)
        );
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode),
            () =>
                Assert.Contains(
                    "chained",
                    problem.RootElement.GetProperty("detail").GetString(),
                    StringComparison.OrdinalIgnoreCase
                )
        );
    }

    /// <summary>Verifies switching impersonation off stops the bearer scheme accepting tokens signed with the impersonation key.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_ImpersonationDisabled_ImpersonationKeyNoLongerAccepted_Test()
    {
        // Arrange
        var token = JwtTestTokens.Create(_factory, UserId, signingKey: ImpersonationKey(_factory));
        using var disabled = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Impersonation:Enabled", "false")
        );
        using var client = ClientWithToken(disabled, token);

        // Act
        using var response = await client.GetAsync(ProductsUri, CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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
}
