using System.Net;
using MediatrUnionPoc.Api.Audit;
using MediatrUnionPoc.Api.IntegrationTests.TestData;
using MediatrUnionPoc.Api.RateLimiting;
using MediatrUnionPoc.Application.Common.Auditing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Events;
using static MediatrUnionPoc.Api.IntegrationTests.TestData.ImpersonationTestSupport;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>
/// Exercises the rate limiter's interaction with impersonation, with the real JWT scheme: a request made
/// under an impersonation token is counted against the real actor (so minting tokens cannot buy budget),
/// minting is limited far more tightly than anything else, and a refused mint is audited even though it never
/// reached the command.
/// </summary>
[Trait("Category", "Integration")]
public sealed class RateLimitingImpersonationTests : IDisposable
{
    private const string AdministratorRole = "Administrator";
    private const string TraceIdHeader = "X-Trace-Id";
    private const string AnonymousAddress = "198.51.100.5";
    private const int AuditWriteFailedEventId = 1301;

    private readonly ProductsApiFactory _factory = new(ApiAuthentication.RealJwt);

    /// <inheritdoc/>
    public void Dispose() => _factory.Dispose();

    /// <summary>Verifies requests under any number of impersonation tokens spend the real administrator's budget, not one budget per target, and that the administrator's own requests draw on the same budget.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task ImpersonatedRequests_CountAgainstTheRealActor_Test()
    {
        // Arrange
        using var factory = _factory.WithLimits(reads: 3, impersonation: 10);
        using var minter = ClientWithToken(
            factory,
            JwtTestTokens.Create(_factory, AdminId, [AdministratorRole])
        );
        using var asAlice = ClientWithToken(
            factory,
            await MintAsync(minter, Body(target: "alice"))
        );
        using var asBob = ClientWithToken(factory, await MintAsync(minter, Body(target: "bob")));
        using var carol = ClientWithToken(factory, JwtTestTokens.Create(_factory, "carol"));

        // Act
        var aliceStatuses = await asAlice.GetStatusesAsync(ApiRoutes.Products, 2);
        var bobStatuses = await asBob.GetStatusesAsync(ApiRoutes.Products, 2);
        var adminStatuses = await minter.GetStatusesAsync(ApiRoutes.Products, 1);
        var carolStatuses = await carol.GetStatusesAsync(ApiRoutes.Products, 1);

        // Assert
        Assert.Multiple(
            () => Assert.Equal([200, 200], aliceStatuses),
            () => Assert.Equal([200, 429], bobStatuses),
            () => Assert.Equal([429], adminStatuses),
            () => Assert.Equal([200], carolStatuses)
        );
    }

    /// <summary>Verifies the impersonated request that is refused is still recorded by the impersonation audit middleware, as a 429 attributed to the real actor.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task ImpersonatedRequest_Refused_IsStillAuditedAs429_Test()
    {
        // Arrange
        using var factory = _factory.WithLimits(reads: 1, impersonation: 10);
        using var minter = ClientWithToken(
            factory,
            JwtTestTokens.Create(_factory, AdminId, [AdministratorRole])
        );
        using var asAlice = ClientWithToken(
            factory,
            await MintAsync(minter, Body(target: "alice"))
        );

        // Act
        var statuses = await asAlice.GetStatusesAsync(ApiRoutes.Products, 2);

        // Assert
        var requestEvents = _factory
            .ReadAuditEvents()
            .Where(e => (string?)e["action"] == ImpersonationAuditMiddleware.ActionName)
            .ToList();
        Assert.Multiple(
            () => Assert.Equal([200, 429], statuses),
            () => Assert.Equal(["200", "429"], requestEvents.Select(e => (string?)e["outcome"])),
            () => Assert.All(requestEvents, e => Assert.Equal(AdminId, (string?)e["actorId"]))
        );
    }

    /// <summary>Verifies minting is limited by its own, tighter budget (per real caller), and that spending it leaves reads and writes alone.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Mint_OverItsOwnTightLimit_Returns429_AndLeavesReadsAlone_Test()
    {
        // Arrange
        using var factory = _factory.WithLimits(impersonation: 2);
        using var admin = ClientWithToken(
            factory,
            JwtTestTokens.Create(_factory, AdminId, [AdministratorRole])
        );
        using var support = ClientWithToken(
            factory,
            JwtTestTokens.Create(_factory, SupportId, ["Support"])
        );

        // Act
        using var first = await PostAsync(admin, Body());
        using var second = await PostAsync(admin, Body());
        using var refused = await PostAsync(admin, Body());
        using var otherCaller = await PostAsync(support, Body());
        using var read = await admin.GetAsync(ApiRoutes.Products, CancellationToken.None);

        // Assert
        var body = await refused.ReadJsonAsync();
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.OK, first.StatusCode),
            () => Assert.Equal(HttpStatusCode.OK, second.StatusCode),
            () => Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode),
            () => Assert.Equal(RateLimitTestSupport.RateLimitedCode, (string?)body["code"]),
            () => Assert.True(refused.RetryAfterSeconds() > 0),
            () => Assert.Equal(HttpStatusCode.OK, otherCaller.StatusCode),
            () => Assert.Equal(HttpStatusCode.OK, read.StatusCode)
        );
    }

    /// <summary>Verifies a refused mint is written to the audit stream (action, RateLimited outcome, the real actor, source address and the response's trace id), with no token or reason in it.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Mint_Refused_IsAudited_Test()
    {
        // Arrange
        using var factory = _factory.WithLimits(impersonation: 1);
        using var admin = ClientWithToken(
            factory,
            JwtTestTokens.Create(_factory, AdminId, [AdministratorRole])
        );

        // Act
        using var issued = await PostAsync(admin, Body());
        using var refused = await PostAsync(admin, Body());

        // Assert
        var refusal = Assert.Single(
            _factory.ReadAuditEvents(),
            e => (string?)e["outcome"] == RateLimitRejectionHandler.AuditOutcome
        );
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode),
            () => Assert.Equal("Impersonation.IssueToken", (string?)refusal["action"]),
            () => Assert.Equal(AdminId, (string?)refusal["actorId"]),
            () => Assert.Equal(AdminId, (string?)refusal["effectiveId"]),
            () => Assert.False((bool)refusal["isImpersonated"]!),
            () => Assert.Equal(ProductsApiFactory.TestRemoteAddress, (string?)refusal["sourceIp"]),
            () =>
                Assert.Equal(
                    refused.Headers.GetValues(TraceIdHeader).Single(),
                    (string?)refusal["traceId"]
                ),
            () => Assert.Equal("Impersonation", (string?)refusal["details"]!["policy"]),
            () => Assert.Null(refusal["reason"]),
            () => Assert.Null(refusal["token"])
        );
    }

    /// <summary>Verifies an anonymous caller hammering the mint endpoint is refused by address and the refusal is audited with no actor.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Mint_AnonymousRefused_IsAuditedWithoutAnActor_Test()
    {
        // Arrange
        using var factory = _factory.WithLimits(impersonation: 1);
        using var client = factory.CreateClient();

        // Act
        using var first = await client.RequestAsync(
            HttpMethod.Post,
            TokensUri,
            AnonymousAddress,
            content: Body()
        );
        using var refused = await client.RequestAsync(
            HttpMethod.Post,
            TokensUri,
            AnonymousAddress,
            content: Body()
        );

        // Assert
        var refusal = Assert.Single(_factory.ReadAuditEvents());
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode),
            () => Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode),
            () => Assert.Equal(RateLimitRejectionHandler.AuditOutcome, (string?)refusal["outcome"]),
            () => Assert.Null(refusal["actorId"]),
            () => Assert.Equal(AnonymousAddress, (string?)refusal["sourceIp"])
        );
    }

    /// <summary>Verifies a refusal by a policy other than Impersonation writes nothing to the audit stream.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Refusal_ByAnotherPolicy_IsNotAudited_Test()
    {
        // Arrange
        using var factory = _factory.WithLimits(reads: 1);
        using var client = ClientWithToken(factory, JwtTestTokens.Create(_factory, UserId));

        // Act
        var statuses = await client.GetStatusesAsync(ApiRoutes.Products, 2);

        // Assert
        Assert.Multiple(
            () => Assert.Equal([200, 429], statuses),
            () => Assert.Empty(_factory.ReadAuditEvents())
        );
    }

    /// <summary>Verifies a failure to write the refusal's audit event is logged at Error and does not change the answer: the caller still gets the 429 problem.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Mint_RefusedWhenTheAuditWriteFails_StillReturns429AndLogsAnError_Test()
    {
        // Arrange
        using var factory = _factory.WithLimits(
            impersonation: 1,
            configure: builder =>
                builder.ConfigureTestServices(services =>
                    services.AddSingleton<IAuditLog>(provider => new FailingOnRefusalAuditLog(
                        provider.GetRequiredService<FileAuditLog>()
                    ))
                )
        );
        using var admin = ClientWithToken(
            factory,
            JwtTestTokens.Create(_factory, AdminId, [AdministratorRole])
        );

        // Act
        using var issued = await PostAsync(admin, Body());
        using var refused = await PostAsync(admin, Body());

        // Assert
        var body = await refused.ReadJsonAsync();
        var failure = Assert.Single(
            _factory.LogSink.Events,
            logEvent => logEvent.EventIdNumber() == AuditWriteFailedEventId
        );
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.OK, issued.StatusCode),
            () => Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode),
            () => Assert.Equal(RateLimitTestSupport.RateLimitedCode, (string?)body["code"]),
            () => Assert.True(refused.RetryAfterSeconds() > 0),
            () => Assert.Equal(LogEventLevel.Error, failure.Level),
            () => Assert.NotNull(failure.Exception),
            () =>
                Assert.DoesNotContain(
                    _factory.ReadAuditEvents(),
                    e => (string?)e["outcome"] == RateLimitRejectionHandler.AuditOutcome
                )
        );
    }

    /// <summary>Verifies a bad bearer token (a 401 by authentication) is counted by address and still spends budget.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task BadToken_StillSpendsTheAddressBudget_Test()
    {
        // Arrange
        using var factory = _factory.WithLimits(reads: 1);
        using var client = ClientWithToken(factory, "not-a-token");

        // Act
        var statuses = await client.GetStatusesAsync(ApiRoutes.Products, 2);

        // Assert
        Assert.Equal([401, 429], statuses);
    }

    /// <summary>An audit log that throws for the rate-limit refusal event and delegates every other event to the real log, so only the refusal's write fails.</summary>
    /// <param name="inner">The real audit log that receives every non-refusal event.</param>
    private sealed class FailingOnRefusalAuditLog(IAuditLog inner) : IAuditLog
    {
        public Task RecordAsync(
            AuditEvent auditEvent,
            CancellationToken cancellationToken = default
        ) =>
            auditEvent?.Outcome == RateLimitRejectionHandler.AuditOutcome
                ? Task.FromException(new IOException("The audit store is unavailable."))
                : inner.RecordAsync(auditEvent!, cancellationToken);
    }
}
