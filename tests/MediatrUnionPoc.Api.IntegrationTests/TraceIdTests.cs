using System.Net;
using System.Text.Json;
using MediatrUnionPoc.Api.IntegrationTests.TestData;
using Microsoft.AspNetCore.Hosting;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>Exercises the request trace id: the <c>X-Trace-Id</c> header, the problem-body <c>traceId</c> member and the logging scope.</summary>
[Trait("Category", "Integration")]
public sealed class TraceIdTests
{
    private const string TraceHeader = "X-Trace-Id";

    /// <summary>Verifies a successful response carries a non-empty trace id header.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_SuccessfulRequest_ResponseHasTraceIdHeader_Test()
    {
        // Arrange
        using var factory = new ProductsApiFactory();
        using var client = factory.CreateClient().AsUser(ProductRequestMother.DefaultCallerId);

        // Act
        using var response = await client.GetAsync("/api/products", CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues(TraceHeader, out var values));
        Assert.False(string.IsNullOrWhiteSpace(Assert.Single(values)));
    }

    /// <summary>Verifies a controller-produced problem body's traceId equals the response header.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_UnknownProduct_ProblemBodyTraceIdEqualsHeader_Test()
    {
        // Arrange
        using var factory = new ProductsApiFactory();
        using var client = factory.CreateClient().AsUser(ProductRequestMother.DefaultCallerId);

        // Act
        using var response = await client.GetAsync(
            $"/api/products/{Guid.NewGuid()}",
            CancellationToken.None
        );

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertBodyTraceIdMatchesHeaderAsync(response);
    }

    /// <summary>Verifies a framework-generated 400 (malformed JSON) carries the header-matching traceId.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Post_MalformedJson_FrameworkProblemBodyHasMatchingTraceId_Test()
    {
        // Arrange
        using var factory = new ProductsApiFactory();
        using var client = factory.CreateClient().AsUser(ProductRequestMother.DefaultCallerId);
        using var content = new StringContent(
            "{ not json",
            System.Text.Encoding.UTF8,
            "application/json"
        );

        // Act
        using var response = await client.PostAsync(
            "/api/products",
            content,
            CancellationToken.None
        );

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertBodyTraceIdMatchesHeaderAsync(response);
    }

    /// <summary>Verifies a routing 404 (no such route) answers a problem body with the header-matching traceId.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_UnroutedPath_ProblemBodyHasMatchingTraceId_Test()
    {
        // Arrange
        using var factory = new ProductsApiFactory();
        using var client = factory.CreateClient().AsUser(ProductRequestMother.DefaultCallerId);

        // Act
        using var response = await client.GetAsync("/no/such/route", CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        await AssertBodyTraceIdMatchesHeaderAsync(response);
    }

    /// <summary>Verifies a 415 (unsupported media type) carries the header-matching traceId.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Post_UnsupportedMediaType_ProblemBodyHasMatchingTraceId_Test()
    {
        // Arrange
        using var factory = new ProductsApiFactory();
        using var client = factory.CreateClient().AsUser(ProductRequestMother.DefaultCallerId);
        using var content = new StringContent("x", System.Text.Encoding.UTF8, "text/plain");

        // Act
        using var response = await client.PostAsync(
            "/api/products",
            content,
            CancellationToken.None
        );

        // Assert
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        await AssertBodyTraceIdMatchesHeaderAsync(response);
    }

    /// <summary>Verifies the pipeline's own log lines carry the same TraceId as the response header.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_SuccessfulRequest_LoggingBehaviorLinesCarryTraceIdProperty_Test()
    {
        // Arrange
        using var factory = new ProductsApiFactory();
        using var client = factory.CreateClient().AsUser(ProductRequestMother.DefaultCallerId);

        // Act
        using var response = await client.GetAsync("/api/products", CancellationToken.None);

        // Assert
        var header = Assert.Single(response.Headers.GetValues(TraceHeader));
        var pipelineLines = factory
            .LogSink.Events.Where(log =>
                log.From("MediatrUnionPoc.Application.Common.Behaviors.LoggingBehavior")
            )
            .ToList();
        Assert.NotEmpty(pipelineLines);
        Assert.All(pipelineLines, line => Assert.Equal(header, line.Scalar("TraceId")));
    }

    private static async Task AssertBodyTraceIdMatchesHeaderAsync(HttpResponseMessage response)
    {
        var header = Assert.Single(response.Headers.GetValues(TraceHeader));
        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(CancellationToken.None)
        );

        Assert.Equal(header, body.RootElement.GetProperty("traceId").GetString());
    }
}
