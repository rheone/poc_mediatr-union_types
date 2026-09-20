using MediatrUnionPoc.Api.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>Exercises <see cref="GlobalExceptionHandler"/> directly for the client-abort path, which ASP.NET Core's own exception middleware may intercept before it reaches the handler in a live host.</summary>
public sealed class GlobalExceptionHandlerTests
{
    /// <summary>Verifies a cancellation while the client has aborted is swallowed: handled, no body, no error log.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task TryHandleAsync_CancellationAfterClientAbort_HandledWithoutBodyOrErrorLog_Test()
    {
        // Arrange
        using var logs = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddProvider(logs).SetMinimumLevel(LogLevel.Debug)
        );
        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();
        var http = new DefaultHttpContext { RequestAborted = aborted.Token };
        var handler = new GlobalExceptionHandler(
            new ThrowingProblemDetailsService(),
            new StubEnvironment(),
            loggerFactory.CreateLogger<GlobalExceptionHandler>()
        );

        // Act
        var handled = await handler.TryHandleAsync(
            http,
            new OperationCanceledException(aborted.Token),
            CancellationToken.None
        );

        // Assert
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status200OK, http.Response.StatusCode);
        Assert.DoesNotContain(logs.Entries, log => log.Level >= LogLevel.Warning);
    }

    private sealed class ThrowingProblemDetailsService : IProblemDetailsService
    {
        public ValueTask WriteAsync(ProblemDetailsContext context) =>
            throw new InvalidOperationException("No body should be written for a client abort.");

        public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context) =>
            throw new InvalidOperationException("No body should be written for a client abort.");
    }

    private sealed class StubEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = "Tests";

        public string ContentRootPath { get; set; } = string.Empty;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
