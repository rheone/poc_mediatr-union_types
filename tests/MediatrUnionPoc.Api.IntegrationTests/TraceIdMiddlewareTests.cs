using System.Diagnostics;
using MediatrUnionPoc.Api.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>Exercises <see cref="TraceIdMiddleware"/> in isolation, where no hosting-provided scope or ambient <see cref="Activity"/> masks its own behavior.</summary>
public sealed class TraceIdMiddlewareTests
{
    /// <summary>Verifies that with no ambient activity the scope opened around the pipeline carries the request's trace identifier as TraceId.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_NoActivity_ScopesLogsWithTraceIdentifier_Test()
    {
        // Arrange
        Assert.Null(Activity.Current);
        using var logs = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddProvider(logs).SetMinimumLevel(LogLevel.Debug)
        );
        var downstream = loggerFactory.CreateLogger("Downstream");
        var middleware = new TraceIdMiddleware(
            _ =>
            {
                downstream.LogInformation("inside the pipeline");
                return Task.CompletedTask;
            },
            loggerFactory.CreateLogger<TraceIdMiddleware>()
        );
        var context = new DefaultHttpContext { TraceIdentifier = "fallback-id-123" };

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var entry = Assert.Single(logs.Entries, log => log.Category == "Downstream");
        Assert.Equal("fallback-id-123", entry.ScopeValue("TraceId"));
    }
}
