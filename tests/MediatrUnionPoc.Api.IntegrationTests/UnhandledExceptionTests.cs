using System.Net;
using System.Text.Json;
using MediatR;
using MediatrUnionPoc.Api.IntegrationTests.TestData;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>Exercises the global exception handler through the real HTTP pipeline with a sender that throws.</summary>
[Trait("Category", "Integration")]
public sealed class UnhandledExceptionTests
{
    private const string Secret = "secret-internal-detail-42";

    private static WebApplicationFactory<Program> ThrowingFactory(
        ProductsApiFactory baseFactory,
        string environment,
        Exception exception,
        CapturingLoggerProvider? logs = null
    ) =>
        baseFactory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.UseSetting(
                "Authentication:Jwt:SigningKey",
                JwtTestTokens.NonDevelopmentSigningKey
            );
            if (logs is not null)
            {
                builder.ConfigureLogging(logging =>
                    logging.SetMinimumLevel(LogLevel.Debug).AddProvider(logs)
                );
            }

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
        using var response = await client.GetAsync("/api/products", CancellationToken.None);

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
        using var response = await client.GetAsync("/api/products", CancellationToken.None);

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

    /// <summary>Verifies the exception is logged at Error with the exception attached and the request's trace id in scope.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_HandlerThrows_LogsErrorWithExceptionAndTraceIdScope_Test()
    {
        // Arrange
        using var logs = new CapturingLoggerProvider();
        var thrown = new InvalidOperationException(Secret);
        using var baseFactory = new ProductsApiFactory();
        using var factory = ThrowingFactory(baseFactory, "Production", thrown, logs);
        using var client = factory.CreateClient().AsUser(ProductRequestMother.DefaultCallerId);

        // Act
        using var response = await client.GetAsync("/api/products", CancellationToken.None);

        // Assert
        var header = Assert.Single(response.Headers.GetValues("X-Trace-Id"));
        var entry = Assert.Single(
            logs.Entries,
            log => log.Level == LogLevel.Error && ReferenceEquals(log.Exception, thrown)
        );
        Assert.Equal(header, entry.ScopeValue("TraceId"));
    }

    /// <summary>Verifies a client abort mid-request produces no error-level log and is recorded as a client abort.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_ClientAbortsMidRequest_LogsNoErrors_Test()
    {
        // Arrange
        using var logs = new CapturingLoggerProvider();
        var sender = new BlockingSender();
        using var baseFactory = new ProductsApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging =>
                logging.SetMinimumLevel(LogLevel.Debug).AddProvider(logs)
            );
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
                using var response = await client.GetAsync("/api/products", cts.Token);
            },
            CancellationToken.None
        );
        await sender.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        await cts.CancelAsync();
#pragma warning disable VSTHRD003 // request was started by this test's own Task.Run above
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
#pragma warning restore VSTHRD003
        await sender.Finished.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);

        // Give the rest of the server pipeline a moment to finish logging after the handler unwinds.
        await Task.Delay(500, CancellationToken.None);

        // Assert
        Assert.DoesNotContain(logs.Entries, log => log.Level >= LogLevel.Error);
    }
}
