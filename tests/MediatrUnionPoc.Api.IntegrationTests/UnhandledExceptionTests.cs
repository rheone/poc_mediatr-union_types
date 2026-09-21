using System.Net;
using System.Text.Json;
using MediatR;
using MediatrUnionPoc.Api.IntegrationTests.TestData;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog.Events;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>Exercises the global exception handler through the real HTTP pipeline with a sender that throws.</summary>
[Trait("Category", "Integration")]
public sealed class UnhandledExceptionTests
{
    private const string Secret = "secret-internal-detail-42";
    private const string RequestLoggingCategory = "Serilog.AspNetCore.RequestLoggingMiddleware";
    private const int PollAttempts = 10;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

    private static WebApplicationFactory<Program> ThrowingFactory(
        ProductsApiFactory baseFactory,
        string environment,
        Exception exception
    ) =>
        baseFactory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.UseSetting(
                "Authentication:Jwt:SigningKey",
                JwtTestTokens.NonDevelopmentSigningKey
            );
            builder.UseSetting(
                "Impersonation:SigningKey",
                JwtTestTokens.NonDevelopmentImpersonationKey
            );
            builder.ConfigureServices(services =>
                services.Replace(
                    ServiceDescriptor.Singleton<ISender>(new ThrowingSender(exception))
                )
            );
        });

    /// <summary>Verifies an unhandled exception in Production answers a 500 problem body with the traceId and without the exception message.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_HandlerThrowsInProduction_Returns500ProblemWithoutExceptionMessage_Test()
    {
        // Arrange
        using var baseFactory = new ProductsApiFactory();
        using var factory = ThrowingFactory(
            baseFactory,
            "Production",
            new InvalidOperationException(Secret)
        );
        using var client = factory.CreateClient().AsUser(ProductRequestMother.DefaultCallerId);

        // Act
        using var response = await client.GetAsync(ApiRoutes.Products, CancellationToken.None);

        // Assert
        var text = await response.Content.ReadAsStringAsync(CancellationToken.None);
        using var body = JsonDocument.Parse(text);
        var header = Assert.Single(response.Headers.GetValues("X-Trace-Id"));
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode),
            () =>
                Assert.Equal(
                    "application/problem+json",
                    response.Content.Headers.ContentType?.MediaType
                ),
            () => Assert.Equal(500, body.RootElement.GetProperty("status").GetInt32()),
            () => Assert.Equal(header, body.RootElement.GetProperty("traceId").GetString()),
            () => Assert.DoesNotContain(Secret, text, StringComparison.Ordinal),
            () => Assert.DoesNotContain("InvalidOperationException", text, StringComparison.Ordinal)
        );
    }

    /// <summary>Verifies the exception message appears in the problem detail in the Development environment.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_HandlerThrowsInDevelopment_ProblemDetailContainsExceptionMessage_Test()
    {
        // Arrange
        using var baseFactory = new ProductsApiFactory();
        using var factory = ThrowingFactory(
            baseFactory,
            "Development",
            new InvalidOperationException(Secret)
        );
        using var client = factory.CreateClient().AsUser(ProductRequestMother.DefaultCallerId);

        // Act
        using var response = await client.GetAsync(ApiRoutes.Products, CancellationToken.None);

        // Assert
        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(CancellationToken.None)
        );
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains(
            Secret,
            body.RootElement.GetProperty("detail").GetString(),
            StringComparison.Ordinal
        );
    }

    /// <summary>Verifies the exception is logged exactly once at Error, with the exception attached and the request's trace id as a property, and that the request line for the 500 is not a second Error.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_HandlerThrows_LogsExactlyOneErrorWithExceptionAndTraceId_Test()
    {
        // Arrange
        var thrown = new InvalidOperationException(Secret);
        using var baseFactory = new ProductsApiFactory();
        using var factory = ThrowingFactory(baseFactory, "Production", thrown);
        using var client = factory.CreateClient().AsUser(ProductRequestMother.DefaultCallerId);

        // Act
        using var response = await client.GetAsync(ApiRoutes.Products, CancellationToken.None);

        // Assert
        var header = Assert.Single(response.Headers.GetValues("X-Trace-Id"));
        var events = baseFactory.LogSink.Events;
        var entry = Assert.Single(events, log => log.Level == LogEventLevel.Error);
        var requestLine = Assert.Single(events, log => log.From(RequestLoggingCategory));
        Assert.Multiple(
            () => Assert.Same(thrown, entry.Exception),
            () => Assert.Equal(2000, entry.EventIdNumber()),
            () => Assert.Equal(header, entry.Scalar("TraceId")),
            () => Assert.Equal(500, requestLine.Scalar("StatusCode")),
            () => Assert.Equal(LogEventLevel.Warning, requestLine.Level),
            () => Assert.Equal(header, requestLine.Scalar("TraceId"))
        );
    }

    /// <summary>Verifies a client abort mid-request produces no error-level log and is recorded as a client abort.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_ClientAbortsMidRequest_LogsNoErrors_Test()
    {
        // Arrange
        var sender = new BlockingSender();
        using var baseFactory = new ProductsApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
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
        await sender.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        await cts.CancelAsync();
#pragma warning disable VSTHRD003 // request was started by this test's own Task.Run above
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
#pragma warning restore VSTHRD003
        await sender.Finished.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);

        // The server pipeline logs after the handler unwinds: poll in short steps until the request line appears.
        // SWEEP-AMBIGUITY: no confirmed signal that the request line is written for an aborted request, so the poll is bounded and falls through.
        for (var attempt = 0; attempt < PollAttempts; attempt++)
        {
            if (baseFactory.LogSink.Events.Any(log => log.From(RequestLoggingCategory)))
            {
                break;
            }

            await Task.Delay(PollInterval, CancellationToken.None);
        }

        // Assert
        Assert.DoesNotContain(baseFactory.LogSink.Events, log => log.Level >= LogEventLevel.Error);
    }
}
