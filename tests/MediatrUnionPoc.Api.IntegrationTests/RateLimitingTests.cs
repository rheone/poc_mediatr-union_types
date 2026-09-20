using System.Net;
using MediatrUnionPoc.Api.IntegrationTests.TestData;
using Serilog.Events;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>
/// Exercises the rate limiter over real HTTP against the real pipeline: the 429 problem's shape, the
/// budgets (reads, writes, per user, per address), where the limiter sits (after CORS and authentication,
/// before authorization) and what it never touches (health, OpenAPI, preflights). Every limit is tiny and
/// every window an hour long, so the tests count requests and never wait.
/// </summary>
[Trait("Category", "Integration")]
public sealed class RateLimitingTests : IDisposable
{
    private const string ProblemJson = "application/problem+json";
    private const string DevOrigin = "http://localhost:5173";

    private readonly ProductsApiFactory _factory = new();

    /// <inheritdoc/>
    public void Dispose() => _factory.Dispose();

    /// <summary>Verifies the first request over the limit is refused with a 429 problem of exactly the documented shape, the trace id header and a whole-second Retry-After, and the ones within the limit were not.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_OverTheReadLimit_Returns429ProblemWithTheExactShape_Test()
    {
        // Arrange
        using var factory = _factory.WithLimits(reads: 2);
        using var client = factory.CreateClient().AsUser("alice");

        // Act
        using var first = await client.GetAsync(ApiRoutes.Products, CancellationToken.None);
        using var second = await client.GetAsync(ApiRoutes.Products, CancellationToken.None);
        using var refused = await client.GetAsync(ApiRoutes.Products, CancellationToken.None);
        var body = await refused.ReadJsonAsync();

        // Assert
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.OK, first.StatusCode),
            () => Assert.Equal(HttpStatusCode.OK, second.StatusCode),
            () => Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode),
            () => Assert.Equal(ProblemJson, refused.Content.Headers.ContentType?.MediaType),
            () =>
                Assert.Equal(
                    ["code", "detail", "status", "title", "traceId", "type"],
                    body.Select(member => member.Key).Order(StringComparer.Ordinal)
                ),
            () =>
                Assert.Equal(
                    "https://tools.ietf.org/html/rfc6585#section-4",
                    (string?)body["type"]
                ),
            () => Assert.Equal("Too Many Requests", (string?)body["title"]),
            () => Assert.Equal(429, (int?)body["status"]),
            () => Assert.Equal(RateLimitTestSupport.RateLimitedCode, (string?)body["code"]),
            () =>
                Assert.Equal(
                    refused.Headers.GetValues("X-Trace-Id").Single(),
                    (string?)body["traceId"]
                ),
            () => Assert.InRange(refused.RetryAfterSeconds(), 1, 3600),
            () =>
                Assert.Contains(
                    $"{refused.RetryAfterSeconds()} seconds",
                    (string?)body["detail"],
                    StringComparison.Ordinal
                )
        );
    }

    /// <summary>Verifies a refusal is logged once at Warning with the policy and the kind of partition, and that neither the address nor the user id's partition key appears in the message or its properties.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_Refused_LogsAWarningWithPolicyAndPartitionKindButNoAddress_Test()
    {
        // Arrange
        using var factory = _factory.WithLimits(reads: 1);
        using var client = factory.CreateClient().AsUser("alice");

        // Act
        using var allowed = await client.GetAsync(ApiRoutes.Products, CancellationToken.None);
        using var refused = await client.GetAsync(ApiRoutes.Products, CancellationToken.None);

        // Assert
        var refusal = Assert.Single(
            _factory.LogSink.Events,
            logEvent => logEvent.EventIdNumber() == 1300
        );
        var everything = refusal.RenderEverything();
        Assert.Multiple(
            () => Assert.Equal(LogEventLevel.Warning, refusal.Level),
            () => Assert.Equal("Reads", refusal.Scalar("RateLimitPolicy")),
            () => Assert.Equal("user", refusal.Scalar("RateLimitPartitionKind")),
            () =>
                Assert.DoesNotContain(
                    ProductsApiFactory.TestRemoteAddress,
                    everything,
                    StringComparison.Ordinal
                ),
            () => Assert.DoesNotContain("user:alice", everything, StringComparison.Ordinal)
        );
    }

    /// <summary>Verifies the problem body never reveals the caller's address or who else is using the API.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_Refused_BodyRevealsNothingAboutTheCallersOrTheirAddress_Test()
    {
        // Arrange
        using var factory = _factory.WithLimits(reads: 1);
        using var client = factory.CreateClient();

        // Act
        using var allowed = await client.RequestAsync(
            HttpMethod.Get,
            ApiRoutes.Products,
            "198.51.100.23"
        );
        using var refused = await client.RequestAsync(
            HttpMethod.Get,
            ApiRoutes.Products,
            "198.51.100.23"
        );
        var text = await refused.Content.ReadAsStringAsync(CancellationToken.None);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode),
            () => Assert.DoesNotContain("198.51.100.23", text, StringComparison.Ordinal),
            () => Assert.DoesNotContain("ip:", text, StringComparison.Ordinal)
        );
    }

    /// <summary>Verifies reads and writes have independent budgets: exhausting one leaves the other untouched.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task ReadsAndWrites_HaveIndependentBudgets_Test()
    {
        // Arrange
        using var factory = _factory.WithLimits(reads: 1, writes: 1);
        using var client = factory.CreateClient().AsUser("alice");

        // Act
        using var read = await client.GetAsync(ApiRoutes.Products, CancellationToken.None);
        using var readRefused = await client.GetAsync(ApiRoutes.Products, CancellationToken.None);
        using var write = await client.PostAsync(
            ApiRoutes.Products,
            Widget("One"),
            CancellationToken.None
        );
        using var writeRefused = await client.PostAsync(
            ApiRoutes.Products,
            Widget("Two"),
            CancellationToken.None
        );
        using var readStillRefused = await client.GetAsync(
            ApiRoutes.Products,
            CancellationToken.None
        );

        // Assert
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.OK, read.StatusCode),
            () => Assert.Equal(HttpStatusCode.TooManyRequests, readRefused.StatusCode),
            () => Assert.Equal(HttpStatusCode.Created, write.StatusCode),
            () => Assert.Equal(HttpStatusCode.TooManyRequests, writeRefused.StatusCode),
            () => Assert.Equal(HttpStatusCode.TooManyRequests, readStillRefused.StatusCode)
        );
    }

    /// <summary>Verifies every mutating verb draws on the Writes budget and every read on Reads: one PUT, PATCH and DELETE each is refused once the writes are spent.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task PutPatchAndDelete_ShareTheWritesBudget_Test()
    {
        // Arrange
        using var factory = _factory.WithLimits(writes: 1);
        using var client = factory.CreateClient().AsUser("alice");
        var id = Guid.NewGuid();

        // Act
        using var spent = await client.PostAsync(
            ApiRoutes.Products,
            Widget("Spent"),
            CancellationToken.None
        );
        using var put = await client.PutAsync(
            $"{ApiRoutes.Products}/{id}",
            Widget("x"),
            CancellationToken.None
        );
        using var mergePatch = new StringContent(
            "{}",
            System.Text.Encoding.UTF8,
            "application/merge-patch+json"
        );
        using var patch = await client.PatchAsync(
            $"{ApiRoutes.Products}/{id}",
            mergePatch,
            CancellationToken.None
        );
        using var delete = await client.DeleteAsync(
            $"{ApiRoutes.Products}/{id}",
            CancellationToken.None
        );
        using var read = await client.GetAsync(
            $"{ApiRoutes.Products}/{id}",
            CancellationToken.None
        );

        // Assert
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.TooManyRequests, put.StatusCode),
            () => Assert.Equal(HttpStatusCode.TooManyRequests, patch.StatusCode),
            () => Assert.Equal(HttpStatusCode.TooManyRequests, delete.StatusCode),
            () => Assert.Equal(HttpStatusCode.NotFound, read.StatusCode)
        );
    }

    /// <summary>Verifies two authenticated users, even from the same address, have independent budgets.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task TwoUsers_HaveIndependentBudgets_Test()
    {
        // Arrange
        using var factory = _factory.WithLimits(reads: 2);
        using var alice = factory.CreateClient().AsUser("alice");
        using var bob = factory.CreateClient().AsUser("bob");

        // Act
        var aliceStatuses = await alice.GetStatusesAsync(ApiRoutes.Products, 3);
        var bobStatuses = await bob.GetStatusesAsync(ApiRoutes.Products, 3);

        // Assert
        Assert.Multiple(
            () => Assert.Equal([200, 200, 429], aliceStatuses),
            () => Assert.Equal([200, 200, 429], bobStatuses)
        );
    }

    /// <summary>Verifies anonymous callers are counted by address (each address has its own budget) and that requests refused with a 401 still spend it.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Anonymous_IsCountedPerAddress_AndA401StillSpendsBudget_Test()
    {
        // Arrange
        using var factory = _factory.WithLimits(reads: 2);
        using var client = factory.CreateClient();

        // Act
        var fromA = await client.GetStatusesAsync(ApiRoutes.Products, 3, "198.51.100.1");
        var fromB = await client.GetStatusesAsync(ApiRoutes.Products, 3, "198.51.100.2");

        // Assert
        Assert.Multiple(
            () => Assert.Equal([401, 401, 429], fromA),
            () => Assert.Equal([401, 401, 429], fromB)
        );
    }

    /// <summary>Verifies an anonymous address budget and a user budget never mix: exhausting the address leaves an authenticated user untouched, even a user whose id is that very address text.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Anonymous_CannotSpendAnAuthenticatedCallersBudget_AndAKeyCannotCollide_Test()
    {
        // Arrange
        const string address = "198.51.100.1";
        using var factory = _factory.WithLimits(reads: 1);
        using var anonymous = factory.CreateClient();
        using var alice = factory.CreateClient().AsUser("alice");
        using var lookalike = factory.CreateClient().AsUser(address);

        // Act
        var anonymousStatuses = await anonymous.GetStatusesAsync(ApiRoutes.Products, 2, address);
        var aliceStatuses = await alice.GetStatusesAsync(ApiRoutes.Products, 2, address);
        var lookalikeStatuses = await lookalike.GetStatusesAsync(ApiRoutes.Products, 2, address);

        // Assert
        Assert.Multiple(
            () => Assert.Equal([401, 429], anonymousStatuses),
            () => Assert.Equal([200, 429], aliceStatuses),
            () => Assert.Equal([200, 429], lookalikeStatuses)
        );
    }

    /// <summary>Verifies an authenticated caller who has no subject claim falls back to the address budget rather than sharing one bucket with every other such caller.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Authenticated_WithNoSubject_FallsBackToTheAddressPartition_Test()
    {
        // Arrange
        using var factory = _factory.WithLimits(reads: 1);
        using var client = factory.CreateClient().AsUser(null);

        // Act
        var fromA = await client.GetStatusesAsync(ApiRoutes.Products, 2, "198.51.100.1");
        var fromB = await client.GetStatusesAsync(ApiRoutes.Products, 2, "198.51.100.2");

        // Assert
        Assert.Multiple(
            () => Assert.Equal([200, 429], fromA),
            () => Assert.Equal([200, 429], fromB)
        );
    }

    /// <summary>Verifies a request the authorization middleware refuses with a 403 still spends budget, because the limiter runs before authorization.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Delete_ForbiddenRequests_StillSpendWritesBudget_Test()
    {
        // Arrange
        using var factory = _factory.WithLimits(writes: 2);
        using var client = factory.CreateClient().AsUser("alice");
        var uri = $"{ApiRoutes.Products}/{Guid.NewGuid()}";

        // Act
        using var first = await client.DeleteAsync(uri, CancellationToken.None);
        using var second = await client.DeleteAsync(uri, CancellationToken.None);
        using var third = await client.DeleteAsync(uri, CancellationToken.None);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.Forbidden, first.StatusCode),
            () => Assert.Equal(HttpStatusCode.Forbidden, second.StatusCode),
            () => Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode)
        );
    }

    /// <summary>Verifies the health probes and the Development OpenAPI document are never limited, however many requests arrive.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task HealthAndOpenApi_AreNeverLimited_Test()
    {
        // Arrange
        using var factory = _factory.WithLimits(reads: 1, writes: 1, impersonation: 1);
        using var client = factory.CreateClient();

        // Act
        var live = await client.GetStatusesAsync("/health/live", 5);
        var ready = await client.GetStatusesAsync("/health/ready", 5);
        var openApi = await client.GetStatusesAsync(ApiRoutes.OpenApiV1, 5);

        // Assert
        Assert.Multiple(
            () => Assert.All(live, status => Assert.Equal(200, status)),
            () => Assert.All(ready, status => Assert.Equal(200, status)),
            () => Assert.All(openApi, status => Assert.Equal(200, status))
        );
    }

    /// <summary>Verifies a CORS preflight is answered before the limiter: it neither spends budget nor is refused when the budget is already spent.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Preflight_IsNeverCountedOrRefused_Test()
    {
        // Arrange
        using var factory = _factory.WithLimits(reads: 1, writes: 1);
        using var client = factory.CreateClient().AsUser("alice");

        // Act
        var before = await PreflightsAsync(client, 5);
        using var allowed = await client.GetAsync(ApiRoutes.Products, CancellationToken.None);
        using var refused = await client.GetAsync(ApiRoutes.Products, CancellationToken.None);
        var after = await PreflightsAsync(client, 5);

        // Assert
        Assert.Multiple(
            () => Assert.All(before, status => Assert.Equal(204, status)),
            () => Assert.Equal(HttpStatusCode.OK, allowed.StatusCode),
            () => Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode),
            () => Assert.All(after, status => Assert.Equal(204, status))
        );
    }

    /// <summary>Verifies a 429 carries the CORS headers, and that Retry-After is on the exposed list, so browser code can read how long to wait.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Refusal_CrossOrigin_ExposesRetryAfterToBrowserCode_Test()
    {
        // Arrange
        using var factory = _factory.WithLimits(reads: 1);
        using var client = factory.CreateClient().AsUser("alice");

        // Act
        using var allowed = await client.RequestAsync(
            HttpMethod.Get,
            ApiRoutes.Products,
            origin: DevOrigin
        );
        using var refused = await client.RequestAsync(
            HttpMethod.Get,
            ApiRoutes.Products,
            origin: DevOrigin
        );

        // Assert
        var exposed = string.Join(',', refused.Headers.GetValues("Access-Control-Expose-Headers"))
            .Split(',', StringSplitOptions.TrimEntries);
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode),
            () =>
                Assert.Equal(
                    DevOrigin,
                    refused.Headers.GetValues("Access-Control-Allow-Origin").Single()
                ),
            () => Assert.Contains("Retry-After", exposed, StringComparer.OrdinalIgnoreCase),
            () => Assert.True(refused.RetryAfterSeconds() > 0)
        );
    }

    /// <summary>Verifies an unlimited host (limits at their ceiling) is unaffected: many requests, no 429.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_WithGenerousLimits_IsNeverRefused_Test()
    {
        // Arrange
        using var client = _factory.CreateClient().AsUser("alice");

        // Act
        var statuses = await client.GetStatusesAsync(ApiRoutes.Products, 25);

        // Assert
        Assert.All(statuses, status => Assert.Equal(200, status));
    }

    private static async Task<IReadOnlyList<int>> PreflightsAsync(HttpClient client, int count)
    {
        List<int> statuses = [];
        for (var i = 0; i < count; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Options, ApiRoutes.Products);
            request.Headers.TryAddWithoutValidation("Origin", DevOrigin);
            request.Headers.TryAddWithoutValidation("Access-Control-Request-Method", "GET");
            using var response = await client.SendAsync(request, CancellationToken.None);
            statuses.Add((int)response.StatusCode);
        }

        return statuses;
    }

    private static StringContent Widget(string name) =>
        new($$"""{"name":"{{name}}","price":1.5}""", System.Text.Encoding.UTF8, "application/json");
}
