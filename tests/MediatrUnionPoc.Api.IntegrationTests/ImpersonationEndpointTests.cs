using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using MediatrUnionPoc.Api.Contracts;
using MediatrUnionPoc.Api.IntegrationTests.TestData;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using static MediatrUnionPoc.Api.IntegrationTests.TestData.ImpersonationTestSupport;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>
/// Exercises <c>POST /api/v1/impersonation/tokens</c> over real HTTP with the real JWT bearer scheme:
/// who may call it, what it validates, which grants it refuses, the off switch, and that the token is
/// never cached or logged.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ImpersonationEndpointTests : IDisposable
{
    private const string ProblemJson = "application/problem+json";
    private const string AdministratorRole = "Administrator";
    private const string SupportRole = "Support";
    private const string MintTicket = "SUP-9";

    private readonly ProductsApiFactory _factory = new(ApiAuthentication.RealJwt);

    /// <summary>Disposes the test's backing <see cref="ProductsApiFactory"/>.</summary>
    public void Dispose() => _factory.Dispose();

    /// <summary>Verifies an anonymous caller gets the fallback policy's 401 problem, never a token.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Post_Anonymous_Returns401Problem_Test()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        using var response = await PostAsync(client, Body());

        // Assert
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode),
            () => Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType)
        );
    }

    /// <summary>Verifies an authenticated user with neither role is refused with a 403 problem.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Post_PlainUser_Returns403Problem_Test()
    {
        // Arrange
        using var client = ClientAs(_factory, UserId);

        // Act
        using var response = await PostAsync(client, Body(target: "bob"));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode),
            () => Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType)
        );
    }

    /// <summary>Verifies a support user can mint a token for a plain identity, and gets the token, its expiry and the effective identity back.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Post_SupportForPlainIdentity_Returns200WithToken_Test()
    {
        // Arrange
        using var client = ClientAs(_factory, SupportId, SupportRole);

        // Act
        using var response = await PostAsync(client, Body(ticket: "SUP-1"));

        // Assert
        var body = (await response.Content.ReadFromJsonAsync<JsonObject>(CancellationToken.None))!;
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.OK, response.StatusCode),
            () => Assert.False(string.IsNullOrEmpty(body["token"]?.GetValue<string>())),
            () => Assert.Equal(UserId, body["userId"]!.GetValue<string>()),
            () => Assert.Equal(SupportId, body["actorId"]!.GetValue<string>()),
            () => Assert.Equal("Bearer", body["tokenType"]!.GetValue<string>()),
            () => Assert.Empty(body["roles"]!.AsArray()),
            // SWEEP-AMBIGUITY: compares against the real clock; the host has no injectable TimeProvider for token expiry.
            () => Assert.True(body["expiresAt"]!.GetValue<DateTimeOffset>() > DateTimeOffset.UtcNow)
        );
    }

    /// <summary>Verifies a support user may grant a role they hold themselves.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Post_SupportGrantingSupportRole_Returns200_Test()
    {
        // Arrange
        using var client = ClientAs(_factory, SupportId, SupportRole);

        // Act
        using var response = await PostAsync(client, Body(roles: [SupportRole]));

        // Assert
        var body = (await response.Content.ReadFromJsonAsync<JsonObject>(CancellationToken.None))!;
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.OK, response.StatusCode),
            () =>
                Assert.Equal(
                    SupportRole,
                    Assert.Single(body["roles"]!.AsArray())!.GetValue<string>()
                )
        );
    }

    /// <summary>Verifies a support user cannot mint an Administrator token: no privilege escalation.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Post_SupportGrantingAdministrator_Returns403WithReason_Test()
    {
        // Arrange
        using var client = ClientAs(_factory, SupportId, SupportRole);

        // Act
        using var response = await PostAsync(client, Body(roles: [AdministratorRole]));

        // Assert
        using var problem = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(CancellationToken.None)
        );
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode),
            () =>
                Assert.Contains(
                    AdministratorRole,
                    problem.RootElement.GetProperty("detail").GetString(),
                    StringComparison.Ordinal
                )
        );
    }

    /// <summary>Verifies an administrator may mint an Administrator token.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Post_AdministratorGrantingAdministrator_Returns200_Test()
    {
        // Arrange
        using var client = ClientAs(_factory, AdminId, AdministratorRole);

        // Act
        using var response = await PostAsync(client, Body(roles: [AdministratorRole]));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Verifies a role outside the configured assignable list is refused even for an administrator.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Post_AdministratorGrantingUnassignableRole_Returns403_Test()
    {
        // Arrange
        using var client = ClientAs(_factory, AdminId, AdministratorRole);

        // Act
        using var response = await PostAsync(client, Body(roles: ["SuperUser"]));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Verifies a missing, short or control-character reason is a 400 problem with a per-field <c>Reason</c> error.</summary>
    /// <param name="reason">The reason to send.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("too short")]
    [InlineData("a reason\nwith a forged log line")]
    public async Task Post_InvalidReason_Returns400WithReasonError_Test(string? reason)
    {
        // Arrange
        using var client = ClientAs(_factory, AdminId, AdministratorRole);

        // Act
        using var response = await PostAsync(client, Body(reason: reason));

        // Assert
        using var problem = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(CancellationToken.None)
        );
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode),
            () => Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType),
            () =>
                Assert.True(
                    problem.RootElement.GetProperty("errors").TryGetProperty("Reason", out _)
                )
        );
    }

    /// <summary>Verifies a lifetime above the configured maximum, or not positive, is a 400 problem on <c>LifetimeMinutes</c>.</summary>
    /// <param name="minutes">The lifetime to request.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData(61)]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Post_LifetimeOutOfRange_Returns400WithLifetimeError_Test(int minutes)
    {
        // Arrange
        using var client = ClientAs(_factory, AdminId, AdministratorRole);

        // Act
        using var response = await PostAsync(client, Body(lifetimeMinutes: minutes));

        // Assert
        using var problem = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(CancellationToken.None)
        );
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode),
            () =>
                Assert.True(
                    problem
                        .RootElement.GetProperty("errors")
                        .TryGetProperty("LifetimeMinutes", out _)
                )
        );
    }

    /// <summary>Verifies a missing target and impersonating yourself are both 400 problems on <c>TargetUserId</c>.</summary>
    /// <param name="target">The target to send.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    [InlineData(AdminId)]
    public async Task Post_MissingOrSelfTarget_Returns400WithTargetError_Test(string? target)
    {
        // Arrange
        using var client = ClientAs(_factory, AdminId, AdministratorRole);

        // Act
        using var response = await PostAsync(client, Body(target: target));

        // Assert
        using var problem = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(CancellationToken.None)
        );
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode),
            () =>
                Assert.True(
                    problem.RootElement.GetProperty("errors").TryGetProperty("TargetUserId", out _)
                )
        );
    }

    /// <summary>Verifies the default lifetime applies when none is requested, and a requested one is honoured.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Post_LifetimeRequestedOrDefaulted_ExpiresAccordingly_Test()
    {
        // Arrange
        using var client = ClientAs(_factory, AdminId, AdministratorRole);

        // SWEEP-AMBIGUITY: the issuer stamps expiry from the real clock (no injectable TimeProvider is wired into the host), so the assertion uses a one-minute tolerance around wall-clock time.
        var before = DateTimeOffset.UtcNow;

        // Act
        var byDefault = await ExpiryAsync(client, Body());
        var requested = await ExpiryAsync(client, Body(lifetimeMinutes: 5));

        // Assert
        Assert.Multiple(
            () =>
                Assert.InRange(
                    byDefault - before,
                    TimeSpan.FromMinutes(14),
                    TimeSpan.FromMinutes(16)
                ),
            () =>
                Assert.InRange(requested - before, TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(6))
        );
    }

    /// <summary>Verifies the response, success or failure, is marked <c>Cache-Control: no-store</c>.</summary>
    /// <param name="reason">A valid reason for a 200, an invalid one for a 400.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData(ValidReason)]
    [InlineData("short")]
    public async Task Post_Response_IsNoStore_Test(string reason)
    {
        // Arrange
        using var client = ClientAs(_factory, AdminId, AdministratorRole);

        // Act
        using var response = await PostAsync(client, Body(reason: reason));

        // Assert
        Assert.Multiple(
            () => Assert.True(response.Headers.CacheControl?.NoStore),
            () =>
                Assert.Contains(
                    "no-cache",
                    response.Headers.Pragma.ToString(),
                    StringComparison.Ordinal
                )
        );
    }

    /// <summary>Verifies with the switch off the endpoint answers a 404 problem with a trace id to an administrator and to a plain user alike, and still 401 to an anonymous caller.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Post_Disabled_Returns404ToEveryAuthenticatedCallerAnd401ToAnonymous_Test()
    {
        // Arrange
        using var disabled = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Impersonation:Enabled", "false")
        );
        using var adminOnDisabled = ClientWithToken(
            disabled,
            JwtTestTokens.Create(_factory, AdminId, [AdministratorRole])
        );
        using var userOnDisabled = ClientWithToken(
            disabled,
            JwtTestTokens.Create(_factory, UserId)
        );
        using var anonymous = disabled.CreateClient();

        // Act
        using var forAdmin = await PostAsync(adminOnDisabled, Body());
        using var forUser = await PostAsync(userOnDisabled, Body());
        using var forAnonymous = await PostAsync(anonymous, Body());

        // Assert
        using var problem = JsonDocument.Parse(
            await forAdmin.Content.ReadAsStringAsync(CancellationToken.None)
        );
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.NotFound, forAdmin.StatusCode),
            () => Assert.Equal(HttpStatusCode.NotFound, forUser.StatusCode),
            () => Assert.Equal(HttpStatusCode.Unauthorized, forAnonymous.StatusCode),
            () => Assert.Equal(ProblemJson, forAdmin.Content.Headers.ContentType?.MediaType),
            () => Assert.Equal(404, problem.RootElement.GetProperty("status").GetInt32()),
            () =>
                Assert.Equal(
                    forAdmin.Headers.GetValues("X-Trace-Id").Single(),
                    problem.RootElement.GetProperty("traceId").GetString()
                )
        );
    }

    /// <summary>Verifies the operational log never contains the token, the reason or the ticket of a mint: those belong to the audit stream only.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Post_IssuedAndDenied_OperationalLogHasNeitherTheTokenNorTheAuditContent_Test()
    {
        // Arrange
        using var support = ClientWithToken(
            _factory,
            JwtTestTokens.Create(_factory, SupportId, [SupportRole])
        );
        var token = await MintAsync(support, Body(roles: [SupportRole], ticket: MintTicket));

        // Act
        using var denied = await PostAsync(support, Body(roles: [AdministratorRole]));
        using var withToken = ClientWithToken(_factory, token);
        using var listing = await withToken.GetAsync(ApiRoutes.Products, CancellationToken.None);

        // Assert
        var everything = _factory.LogSink.Events.Select(entry => entry.RenderEverything()).ToList();
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.OK, listing.StatusCode),
            () => Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode),
            () =>
                Assert.DoesNotContain(
                    everything,
                    line => line.Contains(token, StringComparison.Ordinal)
                ),
            () =>
                Assert.DoesNotContain(
                    everything,
                    line =>
                        line.Contains(ValidReason, StringComparison.Ordinal)
                        || line.Contains(MintTicket, StringComparison.Ordinal)
                )
        );
    }

    /// <summary>Verifies the OpenAPI document declares the endpoint, its responses and the example bodies.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Document_TokensOperation_DeclaresResponsesAndExamples_Test()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        var document = (
            await client.GetFromJsonAsync<JsonObject>(ApiRoutes.OpenApiV1, CancellationToken.None)
        )!;

        // Assert
        var operation = document["paths"]![TokensUri]!["post"]!;
        var responses = operation["responses"]!.AsObject().Select(p => p.Key).Order().ToList();
        var schemas = document["components"]!["schemas"]!.AsObject();
        Assert.Multiple(
            () => Assert.Equal(["200", "400", "401", "403", "404", "429", "500", "504"], responses),
            () => Assert.NotNull(schemas["IssueImpersonationTokenRequest"]!["examples"]),
            () => Assert.NotNull(schemas["ImpersonationToken"]!["examples"])
        );
    }

    private static async Task<DateTimeOffset> ExpiryAsync(
        HttpClient client,
        IssueImpersonationTokenRequest body
    )
    {
        using var response = await PostAsync(client, body);
        response.EnsureSuccessStatusCode();
        var json = (await response.Content.ReadFromJsonAsync<JsonObject>(CancellationToken.None))!;
        return json["expiresAt"]!.GetValue<DateTimeOffset>();
    }
}
