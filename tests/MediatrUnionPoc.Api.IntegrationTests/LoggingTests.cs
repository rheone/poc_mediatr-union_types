using System.Net;
using MediatrUnionPoc.Api.IntegrationTests.TestData;
using Serilog.Events;
using static MediatrUnionPoc.Api.IntegrationTests.TestData.ImpersonationTestSupport;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>
/// Exercises the Serilog setup through the real host: enrichment of ordinary <c>ILogger</c> events,
/// the one-line request log, configuration-driven levels, fail-fast configuration and what is never
/// logged. Captured with <see cref="CapturingLogEventSink"/>, the same events the file and console
/// sinks receive.
/// </summary>
[Trait("Category", "Integration")]
public sealed class LoggingTests
{
    private const string HandlingTemplate = "Handling {RequestName}";
    private const string RequestLoggingCategory = "Serilog.AspNetCore.RequestLoggingMiddleware";
    private const string ProductsUri = "/api/products";

    /// <summary>
    /// Verifies an ordinary <c>ILogger</c> call from the MediatR pipeline carries the enriched
    /// properties: the request's trace id (the <c>TraceId</c> scope surfaces as a real property), the
    /// user id, the impersonation flag, application, version and environment, machine, process and
    /// thread, and the stable event id.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_OrdinaryLoggerCall_CarriesTraceIdUserAndHostProperties_Test()
    {
        // Arrange
        using var factory = new ProductsApiFactory();
        using var client = factory.CreateClient().AsUser("alice");

        // Act
        using var response = await client.GetAsync(ProductsUri, CancellationToken.None);

        // Assert
        var header = Assert.Single(response.Headers.GetValues("X-Trace-Id"));
        var handling = Assert.Single(
            factory.LogSink.Events,
            log => log.Template() == HandlingTemplate
        );
        Assert.Multiple(
            () => Assert.Equal(header, handling.Scalar("TraceId")),
            () => Assert.Equal("alice", handling.Scalar("UserId")),
            () => Assert.False(handling.Flag("IsImpersonated")),
            () => Assert.False(handling.Properties.ContainsKey("ImpersonatedBy")),
            () => Assert.Equal("GetPagedProductsQuery", handling.Scalar("RequestName")),
            () => Assert.Equal(1000, handling.EventIdNumber()),
            () => Assert.Equal("MediatrUnionPoc.Api", handling.Scalar("Application")),
            () => Assert.Equal("Development", handling.Scalar("Environment")),
            () => Assert.NotNull(handling.Scalar("ApplicationVersion")),
            () => Assert.False(string.IsNullOrEmpty(handling.Scalar("MachineName") as string)),
            () => Assert.Equal(Environment.ProcessId, handling.Scalar("ProcessId")),
            () => Assert.NotNull(handling.Scalar("ThreadId"))
        );
    }

    /// <summary>Verifies the handled line carries the result case under the same trace id.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_HandledLine_CarriesRequestNameResultCaseAndEventId_Test()
    {
        // Arrange
        using var factory = new ProductsApiFactory();
        using var client = factory.CreateClient().AsUser("alice");

        // Act
        using var response = await client.GetAsync(ProductsUri, CancellationToken.None);

        // Assert
        var header = Assert.Single(response.Headers.GetValues("X-Trace-Id"));
        var handled = Assert.Single(
            factory.LogSink.Events,
            log => log.Template() == "Handled {RequestName} -> {ResultCase}"
        );
        Assert.Multiple(
            () => Assert.Equal("GetPagedProductsQuery", handled.Scalar("RequestName")),
            () => Assert.Equal("PagedResult`1", handled.Scalar("ResultCase")),
            () => Assert.Equal(1001, handled.EventIdNumber()),
            () => Assert.Equal(header, handled.Scalar("TraceId"))
        );
    }

    /// <summary>Verifies that under a real impersonation token the events carry the target as the user, the impersonation flag and the real caller as <c>ImpersonatedBy</c>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_UnderImpersonationToken_EventsCarryTargetFlagAndActor_Test()
    {
        // Arrange
        using var factory = new ProductsApiFactory(ApiAuthentication.RealJwt);
        using var minter = ClientAs(factory, AdminId, "Administrator");
        var token = await MintAsync(minter, Body(roles: ["Support"]));
        using var client = ClientWithToken(factory, token);

        // Act
        using var response = await client.GetAsync(ProductsUri, CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var header = Assert.Single(response.Headers.GetValues("X-Trace-Id"));
        var handling = Assert.Single(
            factory.LogSink.Events,
            log =>
                log.Template() == HandlingTemplate
                && log.Scalar("TraceId") is string id
                && id == header
        );
        var requestLine = Assert.Single(
            factory.LogSink.Events,
            log =>
                log.From(RequestLoggingCategory)
                && log.Scalar("TraceId") is string id
                && id == header
        );
        Assert.Multiple(
            () => Assert.Equal(UserId, handling.Scalar("UserId")),
            () => Assert.True(handling.Flag("IsImpersonated")),
            () => Assert.Equal(AdminId, handling.Scalar("ImpersonatedBy")),
            () => Assert.Equal(UserId, requestLine.Scalar("UserId")),
            () => Assert.True(requestLine.Flag("IsImpersonated")),
            () => Assert.Equal(AdminId, requestLine.Scalar("ImpersonatedBy"))
        );
    }

    /// <summary>Verifies each request writes one structured request line with status, elapsed time, trace id and the caller.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_Request_WritesOneRequestLineWithStatusElapsedAndTraceId_Test()
    {
        // Arrange
        using var factory = new ProductsApiFactory();
        using var client = factory.CreateClient().AsUser("alice");

        // Act
        using var response = await client.GetAsync(ProductsUri, CancellationToken.None);

        // Assert
        var header = Assert.Single(response.Headers.GetValues("X-Trace-Id"));
        var line = Assert.Single(factory.LogSink.Events, log => log.From(RequestLoggingCategory));
        Assert.Multiple(
            () => Assert.Equal(LogEventLevel.Information, line.Level),
            () => Assert.Equal("GET", line.Scalar("RequestMethod")),
            () => Assert.Equal(ProductsUri, line.Scalar("RequestPath")),
            () => Assert.Equal(200, line.Scalar("StatusCode")),
            () => Assert.IsType<double>(line.Scalar("Elapsed")),
            () => Assert.Equal(header, line.Scalar("TraceId")),
            () => Assert.Equal("alice", line.Scalar("UserId")),
            () => Assert.False(line.Flag("IsImpersonated")),
            () => Assert.Equal("MediatrUnionPoc.Api", line.Scalar("Application"))
        );
    }

    /// <summary>Verifies a rejected (anonymous) request still writes its request line, without an invented identity.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_Anonymous_RequestLineHasStatus401AndNoUserProperties_Test()
    {
        // Arrange
        using var factory = new ProductsApiFactory(ApiAuthentication.RealJwt);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync(ProductsUri, CancellationToken.None);

        // Assert
        var line = Assert.Single(factory.LogSink.Events, log => log.From(RequestLoggingCategory));
        Assert.Multiple(
            () => Assert.Equal(401, line.Scalar("StatusCode")),
            () => Assert.False(line.Properties.ContainsKey("UserId")),
            () => Assert.False(line.Properties.ContainsKey("IsImpersonated"))
        );
    }

    /// <summary>Verifies health probes write their request line at Debug, so they are silent at the default level.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_HealthProbe_DefaultLevelWritesNoRequestLine_Test()
    {
        // Arrange
        using var factory = new ProductsApiFactory();
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/health/live", CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(factory.LogSink.Events, log => log.From(RequestLoggingCategory));
    }

    /// <summary>Verifies that with Debug enabled by configuration a health probe's request line appears at Debug while an API request's stays at Information.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_HealthProbeAtDebugLevel_RequestLineIsDebugAndApiLineIsInformation_Test()
    {
        // Arrange
        using var baseFactory = new ProductsApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.UseSetting("Serilog:MinimumLevel:Default", "Debug")
        );
        using var client = factory.CreateClient().AsUser("alice");

        // Act
        using var live = await client.GetAsync("/health/live", CancellationToken.None);
        using var ready = await client.GetAsync("/health/ready", CancellationToken.None);
        using var products = await client.GetAsync(ProductsUri, CancellationToken.None);

        // Assert
        var lines = baseFactory
            .LogSink.Events.Where(log => log.From(RequestLoggingCategory))
            .ToList();
        Assert.Multiple(
            () => Assert.Equal(3, lines.Count),
            () =>
                Assert.All(
                    lines.Where(line =>
                        ((string?)line.Scalar("RequestPath"))!.StartsWith("/health")
                    ),
                    line => Assert.Equal(LogEventLevel.Debug, line.Level)
                ),
            () =>
                Assert.Equal(
                    LogEventLevel.Information,
                    lines.Single(line => (string?)line.Scalar("RequestPath") == ProductsUri).Level
                )
        );
    }

    /// <summary>Verifies a level override in configuration is honoured: silencing the MediatR behaviors removes their lines but not the request line.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_CategoryOverrideInConfiguration_SilencesThatCategoryOnly_Test()
    {
        // Arrange
        using var baseFactory = new ProductsApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.UseSetting(
                "Serilog:MinimumLevel:Override:MediatrUnionPoc.Application.Common.Behaviors",
                "Warning"
            )
        );
        using var client = factory.CreateClient().AsUser("alice");

        // Act
        using var response = await client.GetAsync(ProductsUri, CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Multiple(
            () =>
                Assert.DoesNotContain(
                    baseFactory.LogSink.Events,
                    log => log.From("MediatrUnionPoc.Application.Common.Behaviors")
                ),
            () => Assert.Single(baseFactory.LogSink.Events, log => log.From(RequestLoggingCategory))
        );
    }

    /// <summary>Verifies the framework's own multi-line request logging is off at the default level (the single Serilog request line replaces it).</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_Request_FrameworkHostingDiagnosticsAreSilent_Test()
    {
        // Arrange
        using var factory = new ProductsApiFactory();
        using var client = factory.CreateClient().AsUser("alice");

        // Act
        using var response = await client.GetAsync(ProductsUri, CancellationToken.None);

        // Assert
        Assert.DoesNotContain(
            factory.LogSink.Events,
            log => log.From("Microsoft.AspNetCore.Hosting.Diagnostics")
        );
    }

    /// <summary>Verifies an invalid level in configuration stops the host from starting rather than logging at some guessed level.</summary>
    [Fact]
    public void CreateClient_InvalidLevelInConfiguration_FailsFast_Test()
    {
        // Arrange
        using var baseFactory = new ProductsApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.UseSetting("Serilog:MinimumLevel:Default", "NotALevel")
        );

        // Act
        var exception = Record.Exception(() =>
        {
            using var client = factory.CreateClient();
        });

        // Assert
        Assert.NotNull(exception);
    }

    /// <summary>
    /// Verifies no captured event, in its message, any property or an attached exception, contains
    /// the bearer token, the impersonation token, or the <c>Authorization</c> header.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Requests_WithBearerTokens_NeverLogTokensOrAuthorizationHeader_Test()
    {
        // Arrange
        using var baseFactory = new ProductsApiFactory(ApiAuthentication.RealJwt);
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.UseSetting("Serilog:MinimumLevel:Default", "Verbose")
        );
        var adminToken = JwtTestTokens.Create(baseFactory, AdminId, ["Administrator"]);
        using var admin = ClientWithToken(factory, adminToken);
        var impersonationToken = await MintAsync(admin, Body(roles: ["Support"]));
        using var impersonated = ClientWithToken(factory, impersonationToken);
        using var badToken = ClientWithToken(factory, "not.a.real-token-value-xyz");

        // Act
        using var ok = await impersonated.GetAsync(ProductsUri, CancellationToken.None);
        using var listing = await admin.GetAsync(ProductsUri, CancellationToken.None);
        using var rejected = await badToken.GetAsync(ProductsUri, CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        var everything = string.Join(
            "\n",
            baseFactory.LogSink.Events.Select(log => log.RenderEverything())
        );
        Assert.NotEmpty(baseFactory.LogSink.Events);
        Assert.Multiple(
            () => Assert.DoesNotContain(adminToken, everything, StringComparison.Ordinal),
            () => Assert.DoesNotContain(impersonationToken, everything, StringComparison.Ordinal),
            () =>
                Assert.DoesNotContain(
                    "not.a.real-token-value-xyz",
                    everything,
                    StringComparison.Ordinal
                ),
            () => Assert.DoesNotContain("Bearer ", everything, StringComparison.Ordinal),
            () =>
                Assert.DoesNotContain(
                    "Authorization",
                    everything,
                    StringComparison.OrdinalIgnoreCase
                ),
            () =>
                Assert.DoesNotContain(
                    baseFactory.LogSink.Events,
                    log => log.Properties.ContainsKey("Authorization")
                )
        );
    }
}
