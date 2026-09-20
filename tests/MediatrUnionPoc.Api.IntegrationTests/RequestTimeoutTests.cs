using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using MediatR;
using MediatrUnionPoc.Api.IntegrationTests.TestData;
using MediatrUnionPoc.Application.Common.Auditing;
using MediatrUnionPoc.Application.Features.Impersonation.IssueToken;
using MediatrUnionPoc.Application.Features.Products.Create;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog.Events;
using static MediatrUnionPoc.Api.IntegrationTests.TestData.ImpersonationTestSupport;
using static MediatrUnionPoc.Api.IntegrationTests.TestData.RequestTimeoutTestSupport;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>
/// Exercises the request timeout through the real pipeline: the exact 504 problem, that exactly one response
/// is produced and nothing is logged as an unhandled error, that a real client abort is still swallowed, and
/// what a timed-out fail-closed impersonation mint does. Every timeout is tens of milliseconds and every slow
/// handler waits on the request's token, so no test waits on the wall clock for its result. These tests
/// assume no debugger is attached (the framework's timeout middleware does nothing under one).
/// </summary>
[Trait("Category", "Integration")]
public sealed class RequestTimeoutTests : IDisposable
{
    private static readonly TimeSpan TestBudget = TimeSpan.FromSeconds(20);

    // Long enough that a warmed-up request reaches the point a test wants to interrupt, short enough to keep the test quick.
    private static readonly TimeSpan WarmDeadline = TimeSpan.FromMilliseconds(300);

    private readonly ProductsApiFactory _factory = new();

    /// <inheritdoc/>
    public void Dispose() => _factory.Dispose();

    /// <summary>Verifies a handler that outlives the timeout is answered with the exact 504 problem (type, title, status, code, trace id in body and header), the handler observed the cancellation, and the test finished long before the generous default would have.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_HandlerOutlivesTimeout_Returns504Problem_Test()
    {
        // Arrange
        var sender = new BlockingSender();
        using var factory = _factory.WithTimeouts(
            Tiny,
            configure: builder =>
                builder.ConfigureServices(services =>
                    services.Replace(ServiceDescriptor.Singleton<ISender>(sender))
                )
        );
        using var client = factory.CreateClient().AsUser(ProductRequestMother.DefaultCallerId);
        var clock = Stopwatch.StartNew();

        // Act
        using var response = await client.GetAsync(ApiRoutes.Products, CancellationToken.None);

        // Assert
        var body = await response.ReadJsonAsync();
        var header = Assert.Single(response.Headers.GetValues("X-Trace-Id"));
        await sender.Finished.Task.WaitAsync(TestBudget, CancellationToken.None);
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode),
            () =>
                Assert.Equal(
                    "application/problem+json",
                    response.Content.Headers.ContentType?.MediaType
                ),
            () =>
                Assert.Equal(
                    "https://tools.ietf.org/html/rfc7231#section-6.6.5",
                    (string?)body["type"]
                ),
            () => Assert.Equal("Gateway Timeout", (string?)body["title"]),
            () => Assert.Equal(504, (int?)body["status"]),
            () =>
                Assert.Equal(
                    "The request did not complete in time and was cancelled.",
                    (string?)body["detail"]
                ),
            () => Assert.Equal(RequestTimeoutCode, (string?)body["code"]),
            () => Assert.Equal(header, (string?)body["traceId"]),
            () => Assert.True(clock.Elapsed < TestBudget, $"Took {clock.Elapsed}.")
        );
    }

    /// <summary>Verifies a timeout is one Warning (event 1400) plus the request line for the 504 (also a Warning), with no Error-level entry and no unhandled-exception entry: the cancellation is handled once and no second response is attempted.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_HandlerOutlivesTimeout_LogsOneWarningAndNoError_Test()
    {
        // Arrange
        var sender = new BlockingSender();
        using var factory = _factory.WithTimeouts(
            Tiny,
            configure: builder =>
                builder.ConfigureServices(services =>
                    services.Replace(ServiceDescriptor.Singleton<ISender>(sender))
                )
        );
        using var client = factory.CreateClient().AsUser(ProductRequestMother.DefaultCallerId);

        // Act
        using var response = await client.GetAsync(ApiRoutes.Products, CancellationToken.None);

        // Assert
        var events = _factory.LogSink.Events;
        var requestLine = Assert.Single(
            events,
            log => log.From("Serilog.AspNetCore.RequestLoggingMiddleware")
        );
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode),
            () => Assert.DoesNotContain(events, log => log.Level >= LogEventLevel.Error),
            () => Assert.DoesNotContain(events, log => log.EventIdNumber() == 2000),
            () => Assert.Single(events, log => log.EventIdNumber() == 1400),
            () =>
                Assert.DoesNotContain(
                    events,
                    log => log.From("Microsoft.AspNetCore.Http.Timeouts")
                ),
            () => Assert.Equal(504, requestLine.Scalar("StatusCode")),
            () => Assert.Equal(LogEventLevel.Warning, requestLine.Level)
        );
    }

    /// <summary>Verifies an action that names the Impersonation policy gets that policy's timeout, even though the default is generous.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_ActionNamingAPolicy_UsesThatPolicyTimeout_Test()
    {
        // Arrange
        using var factory = _factory.WithTimeouts(
            impersonation: Tiny,
            configure: builder => builder.WithTimeoutProbe()
        );
        using var client = factory.CreateClient().AsUser("alice");

        // Act
        using var response = await client.GetAsync("/timeout-probe/named", CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
    }

    /// <summary>Verifies an action with no attribute is covered by the default timeout, while an action that opts out is not timed out even though it works far longer than the timeout.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_UnannotatedAction_IsTimedOut_AndDisabledActionIsNot_Test()
    {
        // Arrange
        using var factory = _factory.WithTimeouts(
            Tiny,
            configure: builder => builder.WithTimeoutProbe()
        );
        using var client = factory.CreateClient().AsUser("alice");

        // Act
        using var unannotated = await client.GetAsync(
            "/timeout-probe/unannotated",
            CancellationToken.None
        );
        using var exempt = await client.GetAsync("/timeout-probe/exempt", CancellationToken.None);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.GatewayTimeout, unannotated.StatusCode),
            () => Assert.Equal(HttpStatusCode.OK, exempt.StatusCode)
        );
    }

    /// <summary>Verifies the health probes are never timed out, even with a one-millisecond default.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_HealthEndpoints_AreNotTimedOut_Test()
    {
        // Arrange
        using var factory = _factory.WithTimeouts(TimeSpan.FromMilliseconds(1));
        using var client = factory.CreateClient();

        // Act
        using var live = await client.GetAsync("/health/live", CancellationToken.None);
        using var ready = await client.GetAsync("/health/ready", CancellationToken.None);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.OK, live.StatusCode),
            () => Assert.Equal(HttpStatusCode.OK, ready.StatusCode)
        );
    }

    /// <summary>Verifies a client that hangs up mid-request is still swallowed silently with the timeout middleware in the pipeline: no 504 is attempted, no timeout warning and no error-level log.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_ClientAbortsBeforeTimeout_IsSwallowedAsBefore_Test()
    {
        // Arrange
        var sender = new BlockingSender();
        using var factory = _factory.WithTimeouts(configure: builder =>
        {
            builder.UseSetting("Serilog:MinimumLevel:Default", "Debug");
            builder.ConfigureServices(services =>
                services.Replace(ServiceDescriptor.Singleton<ISender>(sender))
            );
        });
        using var client = factory.CreateClient().AsUser(ProductRequestMother.DefaultCallerId);
        using var cts = new CancellationTokenSource();

        // Act
        var request = Task.Run(
            async () =>
            {
                using var response = await client.GetAsync(ApiRoutes.Products, cts.Token);
            },
            CancellationToken.None
        );
        await sender.Started.Task.WaitAsync(TestBudget, CancellationToken.None);
        await cts.CancelAsync();
#pragma warning disable VSTHRD003 // request was started by this test's own Task.Run above
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
#pragma warning restore VSTHRD003
        await sender.Finished.Task.WaitAsync(TestBudget, CancellationToken.None);
        await Task.Delay(500, CancellationToken.None);

        // Assert
        var events = _factory.LogSink.Events;
        Assert.Multiple(
            () => Assert.DoesNotContain(events, log => log.Level >= LogEventLevel.Error),
            () => Assert.DoesNotContain(events, log => log.EventIdNumber() == 1400),
            () =>
                Assert.DoesNotContain(events, log => log.From("Microsoft.AspNetCore.Http.Timeouts"))
        );
    }

    /// <summary>
    /// Verifies the fail-closed impersonation mint under a timeout: a mint whose pipeline is cancelled by the
    /// timeout delivers no token (504, no token member in the body), and the audit behavior records exactly one
    /// event for the attempt with the outcome <c>Exception</c> (the pipeline threw the cancellation), never a
    /// success and no error log. A warm-up mint first, so the deadline is met inside the pipeline rather than
    /// during cold start.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Post_MintCancelledByTimeout_DeliversNoTokenAndAuditsTheAttempt_Test()
    {
        // Arrange
        var hang = new HangingMintBehavior();
        using var factory = new ProductsApiFactory(ApiAuthentication.RealJwt);
        using var timed = factory.WithTimeouts(
            impersonation: WarmDeadline,
            configure: builder =>
                builder.ConfigureTestServices(services =>
                    services.AddSingleton<
                        IPipelineBehavior<
                            IssueImpersonationTokenCommand,
                            IssueImpersonationTokenResult
                        >
                    >(hang)
                )
        );
        using var client = ClientWithToken(
            timed,
            JwtTestTokens.Create(factory, AdminId, ["Administrator"])
        );
        using var warmUp = await PostAsync(client, Body());
        var warmUpEvents = factory.ReadAuditEvents().Count;
        hang.Enabled = true;

        // Act
        using var response = await PostAsync(client, Body());

        // Assert
        var text = await response.Content.ReadAsStringAsync(CancellationToken.None);
        var recorded = factory.ReadAuditEvents().Skip(warmUpEvents).ToList();
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.OK, warmUp.StatusCode),
            () => Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode),
            () => Assert.DoesNotContain("\"token\"", text, StringComparison.Ordinal),
            () => Assert.Equal("Exception", (string?)Assert.Single(recorded)["outcome"]),
            () =>
                Assert.Equal(
                    IssueImpersonationTokenCommand.Action,
                    (string?)Assert.Single(recorded)["action"]
                ),
            () =>
                Assert.DoesNotContain(
                    factory.LogSink.Events,
                    log => log.Level >= LogEventLevel.Error
                )
        );
    }

    /// <summary>
    /// Verifies what a slow audit write does to a mint that has already produced its token: the write ignores the
    /// request's token on purpose (a disconnecting client must not cost a committed action its record), and the
    /// timeout only cancels a token, so nothing interrupts the request. It runs on past its deadline, the event is
    /// written, and only then is the token delivered (200): fail-closed holds (no token without its event), and the
    /// deadline does not turn a mint that is already complete into a 504. A warm-up mint first, so the token is
    /// minted before the deadline.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Post_MintWithSlowAuditWrite_OutlivesTimeoutAndDeliversTheTokenOnlyAfterTheEvent_Test()
    {
        // Arrange
        var audit = new GatedAuditLog { Hold = false };
        using var factory = new ProductsApiFactory(ApiAuthentication.RealJwt);
        using var timed = factory.WithTimeouts(
            impersonation: WarmDeadline,
            configure: builder =>
                builder.ConfigureTestServices(services =>
                    services.Replace(ServiceDescriptor.Singleton<IAuditLog>(audit))
                )
        );
        using var client = ClientWithToken(
            timed,
            JwtTestTokens.Create(factory, AdminId, ["Administrator"])
        );
        using var warmUp = await PostAsync(client, Body());
        var warmUpEvents = audit.Recorded.Count;
        audit.Hold = true;

        // Act
        var request = PostAsync(client, Body());
        await audit.Reached.Task.WaitAsync(TestBudget, CancellationToken.None);
        await Task.Delay(WarmDeadline + TimeSpan.FromMilliseconds(400), CancellationToken.None);
        var pendingPastTimeout = !request.IsCompleted;
        var recordedBeforeRelease = audit.Recorded.Count - warmUpEvents;
        audit.Release();
#pragma warning disable VSTHRD003 // request was started by this test above
        using var response = await request;
#pragma warning restore VSTHRD003

        // Assert
        var body = (await response.Content.ReadFromJsonAsync<JsonObject>(CancellationToken.None))!;
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.OK, warmUp.StatusCode),
            () => Assert.True(pendingPastTimeout),
            () => Assert.Equal(0, recordedBeforeRelease),
            () => Assert.Equal(HttpStatusCode.OK, response.StatusCode),
            () => Assert.False(string.IsNullOrEmpty((string?)body["token"])),
            () => Assert.Equal(warmUpEvents + 1, audit.Recorded.Count),
            () => Assert.Equal(nameof(ImpersonationToken), audit.Recorded[^1].Outcome)
        );
    }

    /// <summary>
    /// Verifies a transactional command cancelled by the timeout while its transaction is open (after a warm-up
    /// request, with a timeout long enough that the request reliably reaches the transaction) is answered 504,
    /// creates nothing, and is not reported as an error: cancellation is an expected outcome of a timeout, not a
    /// fault in the handler.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Post_CreateCancelledInsideItsTransaction_Returns504CreatesNothingAndLogsNoError_Test()
    {
        // Arrange
        var hang = new HangingCreateBehavior();
        using var factory = _factory.WithTimeouts(
            TimeSpan.FromSeconds(1),
            configure: builder =>
                builder.ConfigureTestServices(services =>
                    services.AddSingleton<
                        IPipelineBehavior<CreateProductCommand, CreateProductResult>
                    >(hang)
                )
        );
        using var client = factory.CreateClient().AsUser(ProductRequestMother.DefaultCallerId);
        hang.Enabled = false;
        using var warmUp = await client.PostAsJsonAsync(
            ApiRoutes.Products,
            ProductRequestMother.Named("Warm-up"),
            CancellationToken.None
        );
        hang.Enabled = true;

        // Act
        using var response = await client.PostAsJsonAsync(
            ApiRoutes.Products,
            ProductRequestMother.Widget(),
            CancellationToken.None
        );
        hang.Enabled = false;
        using var list = await client.GetAsync(ApiRoutes.Products, CancellationToken.None);

        // Assert
        var page = await list.ReadJsonAsync();
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.Created, warmUp.StatusCode),
            () => Assert.True(hang.Reached.Task.IsCompleted),
            () => Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode),
            () => Assert.Single(page["items"]!.AsArray()),
            () =>
                Assert.DoesNotContain(
                    _factory.LogSink.Events,
                    log => log.Level >= LogEventLevel.Error
                )
        );
    }

    private sealed class HangingCreateBehavior
        : IPipelineBehavior<CreateProductCommand, CreateProductResult>
    {
        public bool Enabled { get; set; }

        public TaskCompletionSource Reached { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<CreateProductResult> Handle(
            CreateProductCommand request,
            RequestHandlerDelegate<CreateProductResult> next,
            CancellationToken cancellationToken
        )
        {
            if (Enabled)
            {
                Reached.TrySetResult();
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            return await next(cancellationToken);
        }
    }

    private sealed class HangingMintBehavior
        : IPipelineBehavior<IssueImpersonationTokenCommand, IssueImpersonationTokenResult>
    {
        public bool Enabled { get; set; }

        public async Task<IssueImpersonationTokenResult> Handle(
            IssueImpersonationTokenCommand request,
            RequestHandlerDelegate<IssueImpersonationTokenResult> next,
            CancellationToken cancellationToken
        )
        {
            if (Enabled)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            return await next(cancellationToken);
        }
    }

    private sealed class GatedAuditLog : IAuditLog
    {
        private readonly TaskCompletionSource _gate = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        private readonly List<AuditEvent> _recorded = [];

        public TaskCompletionSource Reached { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IReadOnlyList<AuditEvent> Recorded
        {
            get
            {
                lock (_recorded)
                {
                    return [.. _recorded];
                }
            }
        }

        public bool Hold { get; set; } = true;

        public void Release() => _gate.TrySetResult();

        public async Task RecordAsync(
            AuditEvent auditEvent,
            CancellationToken cancellationToken = default
        )
        {
            if (Hold)
            {
                Reached.TrySetResult();
#pragma warning disable VSTHRD003 // the gate is completed by the test that owns this log
                await _gate.Task;
#pragma warning restore VSTHRD003
            }

            lock (_recorded)
            {
                _recorded.Add(auditEvent);
            }
        }
    }
}
