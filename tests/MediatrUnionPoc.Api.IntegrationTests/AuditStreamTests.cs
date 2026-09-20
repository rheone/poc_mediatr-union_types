using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using MediatrUnionPoc.Api.IntegrationTests.TestData;
using MediatrUnionPoc.Application.Features.Products.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Serilog.Events;
using static MediatrUnionPoc.Api.IntegrationTests.TestData.ImpersonationTestSupport;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>
/// Exercises the audit stream end to end over real HTTP with the real JWT scheme: every impersonation
/// token attempt (issued, refused by the handler, refused by the policy, refused by validation), every
/// request made under a minted token, the product mutations, the separation from the operational log,
/// and the two failure policies against an audit directory that cannot be written.
/// </summary>
[Trait("Category", "Integration")]
public sealed class AuditStreamTests : IDisposable
{
    private const string ProductsUri = "/api/products";

    private readonly ProductsApiFactory _factory = new(ApiAuthentication.RealJwt);

    /// <summary>Disposes the test's backing <see cref="ProductsApiFactory"/>.</summary>
    public void Dispose() => _factory.Dispose();

    /// <summary>Verifies a minted token is audited with the real caller as actor, the target as effective identity, roles, reason, ticket, the token's own id and the response's trace id.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Post_TokenIssued_IsAuditedWithEverythingButTheToken_Test()
    {
        // Arrange
        using var support = ClientAs(_factory, SupportId, "Support");

        // Act
        using var response = await PostAsync(support, Body(roles: ["Support"], ticket: "SUP-9"));

        // Assert
        var token = (await response.Content.ReadFromJsonAsync<JsonObject>(CancellationToken.None))![
            "token"
        ]!.GetValue<string>();
        var auditEvent = Assert.Single(_factory.ReadAuditEvents());
        var jti = new JsonWebTokenHandler().ReadJsonWebToken(token).Id;
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.OK, response.StatusCode),
            () => Assert.Equal("Impersonation.IssueToken", Text(auditEvent, "action")),
            () => Assert.Equal("ImpersonationToken", Text(auditEvent, "outcome")),
            () => Assert.Equal(SupportId, Text(auditEvent, "actorId")),
            () => Assert.Equal(SupportId, Text(auditEvent, "effectiveId")),
            () => Assert.False(auditEvent["isImpersonated"]!.GetValue<bool>()),
            () => Assert.Equal("User", Text(auditEvent, "targetType")),
            () => Assert.Equal(UserId, Text(auditEvent, "targetId")),
            () => Assert.Equal(ValidReason, Text(auditEvent, "reason")),
            () => Assert.Equal("SUP-9", Text(auditEvent, "ticket")),
            () => Assert.Equal("Support", Text(auditEvent["details"]!.AsObject(), "roles")),
            () => Assert.Equal(jti, Text(auditEvent, "tokenId")),
            () =>
                Assert.Equal(
                    response.Headers.GetValues("X-Trace-Id").Single(),
                    Text(auditEvent, "traceId")
                ),
            () => Assert.Equal(ProductsApiFactory.TestRemoteAddress, Text(auditEvent, "sourceIp")),
            () => Assert.True(Guid.TryParse(Text(auditEvent, "id"), out _)),
            () => Assert.NotNull(auditEvent["timestamp"])
        );
    }

    /// <summary>Verifies a request the handler's rules refuse (a support user asking for an Administrator token) is audited as NotAuthorized with the denial.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Post_DeniedByHandlerRules_IsAudited_Test()
    {
        // Arrange
        using var support = ClientAs(_factory, SupportId, "Support");

        // Act
        using var response = await PostAsync(support, Body(roles: ["Administrator"]));

        // Assert
        var auditEvent = Assert.Single(_factory.ReadAuditEvents());
        var details = auditEvent["details"]!.AsObject();
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode),
            () => Assert.Equal("NotAuthorized", Text(auditEvent, "outcome")),
            () => Assert.Equal(SupportId, Text(auditEvent, "actorId")),
            () => Assert.Equal("Administrator", Text(details, "roles")),
            () =>
                Assert.Contains("Administrator", Text(details, "denial"), StringComparison.Ordinal),
            () => Assert.Null(auditEvent["tokenId"])
        );
    }

    /// <summary>Verifies a caller the <c>Impersonator</c> policy refuses before the handler runs is still audited, with that caller as the actor.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Post_DeniedByPolicy_IsAuditedWithTheCallerAsActor_Test()
    {
        // Arrange
        using var plain = ClientAs(_factory, UserId);

        // Act
        using var response = await PostAsync(plain, Body(target: "bob"));

        // Assert
        var auditEvent = Assert.Single(_factory.ReadAuditEvents());
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode),
            () => Assert.Equal("NotAuthorized", Text(auditEvent, "outcome")),
            () => Assert.Equal(UserId, Text(auditEvent, "actorId")),
            () => Assert.Equal("bob", Text(auditEvent, "targetId")),
            () =>
                Assert.Contains(
                    "Impersonator",
                    Text(auditEvent["details"]!.AsObject(), "denial"),
                    StringComparison.Ordinal
                )
        );
    }

    /// <summary>Verifies a request the validator refuses is audited as ValidationErrors, with the reason field named.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Post_ValidationFails_IsAudited_Test()
    {
        // Arrange
        using var admin = ClientAs(_factory, AdminId, "Administrator");

        // Act
        using var response = await PostAsync(admin, Body(reason: "short"));

        // Assert
        var auditEvent = Assert.Single(_factory.ReadAuditEvents());
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode),
            () => Assert.Equal("ValidationErrors", Text(auditEvent, "outcome")),
            () => Assert.Equal(AdminId, Text(auditEvent, "actorId")),
            () =>
                Assert.Contains(
                    "Reason",
                    Text(auditEvent["details"]!.AsObject(), "validation"),
                    StringComparison.Ordinal
                )
        );
    }

    /// <summary>Verifies each request made under a minted token is audited with that token's id, the reason, the real caller and the identity it ran as, and the query string is not recorded.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Requests_UnderAMintedToken_AreAuditedAndTieBackToTheMint_Test()
    {
        // Arrange
        using var admin = ClientAs(_factory, AdminId, "Administrator");
        using var impersonated = ClientWithToken(
            _factory,
            await MintAsync(admin, Body(ticket: "SUP-1"))
        );

        // Act
        using var listing = await impersonated.GetAsync(
            $"{ProductsUri}?pageSize=5&name=secret-filter",
            CancellationToken.None
        );
        using var missing = await impersonated.GetAsync(
            $"{ProductsUri}/{ProductRequestMother.UnknownId}",
            CancellationToken.None
        );

        // Assert
        var events = _factory.ReadAuditEvents();
        var mint = Assert.Single(events, e => Text(e, "action") == "Impersonation.IssueToken");
        var requests = events.Where(e => Text(e, "action") == "Impersonation.Request").ToList();
        var first = requests[0];
        var second = requests[1];
        Assert.Multiple(
            () => Assert.Equal(2, requests.Count),
            () => Assert.Equal("200", Text(first, "outcome")),
            () => Assert.Equal("GET", Text(first["details"]!.AsObject(), "method")),
            () => Assert.Equal(ProductsUri, Text(first["details"]!.AsObject(), "path")),
            () => Assert.Equal("404", Text(second, "outcome")),
            () =>
                Assert.All(
                    requests,
                    request =>
                    {
                        Assert.Equal(Text(mint, "tokenId"), Text(request, "tokenId"));
                        Assert.Equal(ValidReason, Text(request, "reason"));
                        Assert.Equal("SUP-1", Text(request, "ticket"));
                        Assert.Equal(AdminId, Text(request, "actorId"));
                        Assert.Equal(UserId, Text(request, "effectiveId"));
                        Assert.True(request["isImpersonated"]!.GetValue<bool>());
                        Assert.False(string.IsNullOrEmpty(Text(request, "traceId")));
                        Assert.False(string.IsNullOrEmpty(Text(request, "sourceIp")));
                    }
                ),
            () =>
                Assert.DoesNotContain(
                    ProductsApiFactory.ReadAuditLines(_factory.AuditDirectory),
                    line => line.Contains("secret-filter", StringComparison.Ordinal)
                )
        );
    }

    /// <summary>Verifies a request an impersonated caller is then refused (403) is audited too, since the middleware runs before authorization.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Request_UnderAMintedTokenThatIsForbidden_IsAuditedWith403_Test()
    {
        // Arrange
        using var admin = ClientAs(_factory, AdminId, "Administrator");
        using var impersonated = ClientWithToken(_factory, await MintAsync(admin, Body()));

        // Act
        using var response = await impersonated.DeleteAsync(
            $"{ProductsUri}/{ProductRequestMother.UnknownId}",
            CancellationToken.None
        );

        // Assert
        var request = Assert.Single(
            _factory.ReadAuditEvents(),
            e => Text(e, "action") == "Impersonation.Request"
        );
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode),
            () => Assert.Equal("403", Text(request, "outcome"))
        );
    }

    /// <summary>Verifies ordinary reads, and the anonymous health probes, produce no request events: only impersonated requests are audited.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Requests_NotImpersonated_ProduceNoAuditEvents_Test()
    {
        // Arrange
        using var user = ClientAs(_factory, UserId);
        using var anonymous = _factory.CreateClient();

        // Act
        using var listing = await user.GetAsync(ProductsUri, CancellationToken.None);
        using var live = await anonymous.GetAsync("/health/live", CancellationToken.None);
        using var ready = await anonymous.GetAsync("/health/ready", CancellationToken.None);
        using var unauthenticated = await anonymous.GetAsync(ProductsUri, CancellationToken.None);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.OK, listing.StatusCode),
            () => Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode),
            () => Assert.Empty(_factory.ReadAuditEvents())
        );
    }

    /// <summary>Verifies create, update, patch and delete are each audited with the caller as actor and the product as target, and a create names the id of the product it created.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task ProductMutations_AreAuditedWithActorAndTarget_Test()
    {
        // Arrange
        using var owner = ClientAs(_factory, UserId);
        using var admin = ClientAs(_factory, AdminId, "Administrator");

        // Act
        using var created = await owner.PostAsJsonAsync(
            ProductsUri,
            ProductRequestMother.Widget(),
            CancellationToken.None
        );
        var product = (
            await created.Content.ReadFromJsonAsync<ProductDto>(CancellationToken.None)
        )!;
        using var updated = await SendAsync(
            owner,
            HttpMethod.Put,
            product,
            JsonContent.Create(ProductRequestMother.WidgetPro()),
            product.Version.ToETag()
        );
        using var patched = await SendAsync(
            owner,
            HttpMethod.Patch,
            product,
            new StringContent(
                """{"price":5}""",
                Encoding.UTF8,
                new MediaTypeHeaderValue("application/merge-patch+json")
            ),
            "W/\"2\""
        );
        using var deleted = await SendAsync(admin, HttpMethod.Delete, product, null, null);

        // Assert
        var events = _factory.ReadAuditEvents();
        var id = product.Id.Value.ToString();
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.Created, created.StatusCode),
            () => Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode),
            () => Assert.Equal(HttpStatusCode.OK, patched.StatusCode),
            () => Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode),
            () =>
                Assert.Equal(
                    ["Product.Create", "Product.Update", "Product.Patch", "Product.Delete"],
                    events.Select(e => Text(e, "action"))
                ),
            () =>
                Assert.Equal(
                    ["ProductDto", "ProductDto", "ProductDto", "Success"],
                    events.Select(e => Text(e, "outcome"))
                ),
            () => Assert.All(events, e => Assert.Equal("Product", Text(e, "targetType"))),
            () => Assert.All(events, e => Assert.Equal(id, Text(e, "targetId"))),
            () =>
                Assert.Equal(
                    [UserId, UserId, UserId, AdminId],
                    events.Select(e => Text(e, "actorId"))
                )
        );
    }

    /// <summary>Verifies a product mutation made under an impersonation token is attributed to the real caller and carries the token's id.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task ProductCreate_UnderAMintedToken_IsAttributedToTheRealCaller_Test()
    {
        // Arrange
        using var admin = ClientAs(_factory, AdminId, "Administrator");
        using var impersonated = ClientWithToken(_factory, await MintAsync(admin, Body()));

        // Act
        using var response = await impersonated.PostAsJsonAsync(
            ProductsUri,
            ProductRequestMother.Widget(),
            CancellationToken.None
        );

        // Assert
        var events = _factory.ReadAuditEvents();
        var mint = events[0];
        var create = Assert.Single(events, e => Text(e, "action") == "Product.Create");
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.Created, response.StatusCode),
            () => Assert.Equal(AdminId, Text(create, "actorId")),
            () => Assert.Equal(UserId, Text(create, "effectiveId")),
            () => Assert.True(create["isImpersonated"]!.GetValue<bool>()),
            () => Assert.Equal(Text(mint, "tokenId"), Text(create, "tokenId"))
        );
    }

    /// <summary>Verifies the two streams stay apart: no audit content reaches the operational log capture, and the audit files hold no token, credential or operational log fields.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task AuditAndOperationalStreams_DoNotMix_Test()
    {
        // Arrange
        using var admin = ClientAs(_factory, AdminId, "Administrator");
        var token = await MintAsync(admin, Body(ticket: "SUP-7"));
        using var impersonated = ClientWithToken(_factory, token);

        // Act
        using var create = await impersonated.PostAsJsonAsync(
            ProductsUri,
            ProductRequestMother.Widget(),
            CancellationToken.None
        );
        var operational = _factory.LogSink.Events.Select(e => e.RenderEverything()).ToList();
        var audit = string.Join('\n', ProductsApiFactory.ReadAuditLines(_factory.AuditDirectory));
        var jti = new JsonWebTokenHandler().ReadJsonWebToken(token).Id;

        // Assert
        Assert.Multiple(
            () => Assert.NotEmpty(operational),
            () => Assert.NotEmpty(audit),
            () =>
                Assert.DoesNotContain(
                    operational,
                    line =>
                        line.Contains("Impersonation.IssueToken", StringComparison.Ordinal)
                        || line.Contains("Impersonation.Request", StringComparison.Ordinal)
                        || line.Contains(jti, StringComparison.Ordinal)
                        || line.Contains(ValidReason, StringComparison.Ordinal)
                        || line.Contains("SUP-7", StringComparison.Ordinal)
                ),
            () => Assert.DoesNotContain(token, audit, StringComparison.Ordinal),
            () => Assert.DoesNotContain("Bearer", audit, StringComparison.OrdinalIgnoreCase),
            () => Assert.DoesNotContain("Authorization", audit, StringComparison.OrdinalIgnoreCase),
            () => Assert.DoesNotContain("SourceContext", audit, StringComparison.Ordinal),
            () => Assert.DoesNotContain("RenderedMessage", audit, StringComparison.Ordinal)
        );
    }

    /// <summary>Verifies fail closed: when the audit directory cannot be written the mint answers a 500 problem, no token appears anywhere in the response, no event exists, and an Error is logged.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Post_TokenIssuedButAuditUnwritable_FailsClosedWithoutTheToken_Test()
    {
        // Arrange
        using var broken = WithUnwritableAudit();
        using var admin = ClientWithToken(
            broken,
            JwtTestTokens.Create(_factory, AdminId, ["Administrator"])
        );

        // Act
        using var response = await PostAsync(admin, Body());

        // Assert
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);
        var failure = Assert.Single(
            _factory.LogSink.Events,
            e => e.EventIdNumber() == 1100 && e.Level == LogEventLevel.Error
        );
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode),
            () => Assert.DoesNotContain("eyJ", body, StringComparison.Ordinal),
            () => Assert.DoesNotContain("expiresAt", body, StringComparison.Ordinal),
            () => Assert.DoesNotContain("\"token\"", body, StringComparison.OrdinalIgnoreCase),
            () => Assert.Equal("Impersonation.IssueToken", failure.Scalar("AuditAction")),
            () => Assert.Empty(_factory.ReadAuditEvents())
        );
    }

    /// <summary>Verifies best effort: a product create still succeeds when the audit directory cannot be written, and the failure is logged at Error.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task ProductCreate_AuditUnwritable_StillSucceedsAndLogsError_Test()
    {
        // Arrange
        using var broken = WithUnwritableAudit();
        using var owner = ClientWithToken(broken, JwtTestTokens.Create(_factory, UserId));

        // Act
        using var response = await owner.PostAsJsonAsync(
            ProductsUri,
            ProductRequestMother.Widget(),
            CancellationToken.None
        );

        // Assert
        var failure = Assert.Single(
            _factory.LogSink.Events,
            e => e.EventIdNumber() == 1100 && e.Level == LogEventLevel.Error
        );
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.Created, response.StatusCode),
            () => Assert.Equal("Product.Create", failure.Scalar("AuditAction")),
            () => Assert.NotNull(failure.Exception)
        );
    }

    /// <summary>Verifies the request-event middleware is best effort too: an unwritable audit directory does not change the response of an impersonated request, and an Error is logged.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Request_UnderAMintedTokenAuditUnwritable_ResponseUnaffected_Test()
    {
        // Arrange
        using var admin = ClientAs(_factory, AdminId, "Administrator");
        var token = await MintAsync(admin, Body());
        using var broken = WithUnwritableAudit();
        using var impersonated = ClientWithToken(broken, token);

        // Act
        using var response = await impersonated.GetAsync(ProductsUri, CancellationToken.None);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.OK, response.StatusCode),
            () =>
                Assert.Single(
                    _factory.LogSink.Events,
                    e => e.EventIdNumber() == 1200 && e.Level == LogEventLevel.Error
                )
        );
    }

    // A derived host whose audit "directory" is an existing file, so every write fails with an IOException.
    private WebApplicationFactory<Program> WithUnwritableAudit()
    {
        Directory.CreateDirectory(_factory.AuditDirectory);
        var blocker = Path.Combine(_factory.AuditDirectory, "not-a-directory");
        File.WriteAllText(blocker, "a file where the audit directory should be");

        return _factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Audit:Directory", blocker)
        );
    }

    private static string Text(JsonObject node, string name) => node[name]!.GetValue<string>();

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        ProductDto product,
        HttpContent? content,
        string? ifMatch
    )
    {
        using var request = new HttpRequestMessage(method, $"{ProductsUri}/{product.Id.Value}")
        {
            Content = content,
        };
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return await client.SendAsync(request, CancellationToken.None);
    }
}
