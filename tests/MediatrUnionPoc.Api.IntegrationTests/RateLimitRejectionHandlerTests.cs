using System.Threading.RateLimiting;
using MediatrUnionPoc.Api.Http;
using MediatrUnionPoc.Api.RateLimiting;
using MediatrUnionPoc.Application.Common.Auditing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>Unit tests for the <c>Retry-After</c> value the rejection handler chooses, including its documented fallback when the limiter gives no hint.</summary>
[Trait("Category", "Unit")]
public sealed class RateLimitRejectionHandlerTests
{
    /// <summary>Verifies the limiter's hint is rounded up to whole seconds and never below one.</summary>
    /// <param name="hintSeconds">The limiter's hint.</param>
    /// <param name="expected">The header value.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData(0.2, "1")]
    [InlineData(1.0, "1")]
    [InlineData(1.2, "2")]
    [InlineData(59.01, "60")]
    public async Task OnRejectedAsync_WithAHint_RoundsUpToWholeSeconds_Test(
        double hintSeconds,
        string expected
    )
    {
        // Arrange
        const string policy = "Reads";
        var (handler, _) = CreateHandler();
        var context = ContextFor(policy, TimeSpan.FromSeconds(hintSeconds));

        // Act
        await handler.OnRejectedAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(expected, context.HttpContext.Response.Headers.RetryAfter.ToString());
    }

    /// <summary>Verifies with no hint the whole window of the policy that refused is the answer.</summary>
    /// <param name="policy">The refusing policy.</param>
    /// <param name="expected">The policy's window in seconds.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData("Reads", "60")]
    [InlineData("Writes", "45")]
    [InlineData("Impersonation", "300")]
    public async Task OnRejectedAsync_WithoutAHint_FallsBackToThePolicyWindow_Test(
        string policy,
        string expected
    )
    {
        // Arrange
        var (handler, _) = CreateHandler();
        var context = ContextFor(policy, hint: null);

        // Act
        await handler.OnRejectedAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(expected, context.HttpContext.Response.Headers.RetryAfter.ToString());
    }

    /// <summary>Verifies only a refusal by the Impersonation policy writes an audit event.</summary>
    /// <param name="policy">The refusing policy.</param>
    /// <param name="audited">Whether an event is expected.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData("Reads", false)]
    [InlineData("Writes", false)]
    [InlineData("Impersonation", true)]
    public async Task OnRejectedAsync_AuditsOnlyTheImpersonationPolicy_Test(
        string policy,
        bool audited
    )
    {
        // Arrange
        var (handler, auditLog) = CreateHandler();

        // Act
        await handler.OnRejectedAsync(ContextFor(policy, hint: null), CancellationToken.None);

        // Assert
        Assert.Equal(audited ? 1 : 0, auditLog.Events.Count);
    }

    private static OnRejectedContext ContextFor(string policy, TimeSpan? hint)
    {
        var http = new DefaultHttpContext();
        http.SetEndpoint(
            new Endpoint(
                requestDelegate: null,
                new EndpointMetadataCollection(new EnableRateLimitingAttribute(policy)),
                displayName: "test"
            )
        );

        return new OnRejectedContext { HttpContext = http, Lease = new FakeLease(hint) };
    }

    private static (RateLimitRejectionHandler Handler, RecordingAuditLog AuditLog) CreateHandler()
    {
        var auditLog = new RecordingAuditLog();
        var options = new RateLimitingOptions
        {
            Writes = new RateLimitPolicyOptions { PermitLimit = 1, WindowSeconds = 45 },
            Impersonation = new RateLimitPolicyOptions { PermitLimit = 1, WindowSeconds = 300 },
        };

        return (
            new RateLimitRejectionHandler(
                new NoOpProblemDetailsService(),
                Options.Create(new HttpMappingOptions()),
                Options.Create(options),
                auditLog,
                TimeProvider.System,
                NullLogger<RateLimitRejectionHandler>.Instance
            ),
            auditLog
        );
    }

    /// <summary>A rejected lease that offers a <c>RetryAfter</c> hint only when one is supplied, as a real limiter may not.</summary>
    /// <param name="retryAfter">The hint to expose, or <see langword="null"/> for none.</param>
    private sealed class FakeLease(TimeSpan? retryAfter) : RateLimitLease
    {
        public override bool IsAcquired => false;

        public override IEnumerable<string> MetadataNames =>
            retryAfter is null ? [] : [MetadataName.RetryAfter.Name];

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            metadata = metadataName == MetadataName.RetryAfter.Name ? retryAfter : null;
            return metadata is not null;
        }
    }

    /// <summary>An audit log that keeps every recorded event in memory for assertion.</summary>
    private sealed class RecordingAuditLog : IAuditLog
    {
        public List<AuditEvent> Events { get; } = [];

        public Task RecordAsync(
            AuditEvent auditEvent,
            CancellationToken cancellationToken = default
        )
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }
    }

    /// <summary>A problem-details writer that writes nothing, so a test observes only the headers the handler sets.</summary>
    private sealed class NoOpProblemDetailsService : IProblemDetailsService
    {
        public ValueTask WriteAsync(ProblemDetailsContext context) => ValueTask.CompletedTask;

        public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context) =>
            ValueTask.FromResult(true);
    }
}
