using System.Data.Common;
using System.Net;
using MediatrUnionPoc.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>Exercises the anonymous liveness and readiness endpoints through the real HTTP pipeline.</summary>
[Trait("Category", "Integration")]
public sealed class HealthEndpointTests
{
    private static WebApplicationFactory<Program> BreakableFactory(
        ProductsApiFactory baseFactory,
        DatabaseSwitch databaseSwitch
    ) =>
        baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.ConfigureDbContext<AppDbContext>(options =>
                    options.AddInterceptors(databaseSwitch)
                )
            )
        );

    /// <summary>Verifies liveness answers 200 with the plain-text Healthy status.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetLive_Always_Returns200Healthy_Test()
    {
        // Arrange
        using var factory = new ProductsApiFactory();
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/health/live", CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(CancellationToken.None));
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>Verifies readiness answers 200 with the plain-text Healthy status when the database is reachable.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetReady_DatabaseReachable_Returns200Healthy_Test()
    {
        // Arrange
        using var factory = new ProductsApiFactory();
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/health/ready", CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(CancellationToken.None));
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>Verifies readiness answers 503 with the plain-text Unhealthy status, no check details, when the database cannot be reached.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetReady_DatabaseUnreachable_Returns503UnhealthyWithoutDetails_Test()
    {
        // Arrange
        var databaseSwitch = new DatabaseSwitch();
        using var baseFactory = new ProductsApiFactory();
        using var factory = BreakableFactory(baseFactory, databaseSwitch);
        using var client = factory.CreateClient();
        databaseSwitch.Broken = true;

        // Act
        using var response = await client.GetAsync("/health/ready", CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync(CancellationToken.None));
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>Verifies liveness stays 200 while the database is unreachable, because it runs no checks.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetLive_DatabaseUnreachable_StillReturns200_Test()
    {
        // Arrange
        var databaseSwitch = new DatabaseSwitch();
        using var baseFactory = new ProductsApiFactory();
        using var factory = BreakableFactory(baseFactory, databaseSwitch);
        using var client = factory.CreateClient();
        databaseSwitch.Broken = true;

        // Act
        using var response = await client.GetAsync("/health/live", CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Verifies both endpoints answer without any identity headers, which is how an orchestrator probes them.</summary>
    /// <param name="path">The health endpoint path.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Get_NoIdentityHeaders_IsNotRejected_Test(string path)
    {
        // Arrange
        using var factory = new ProductsApiFactory();
        using var client = factory.CreateClient();
        Assert.False(client.DefaultRequestHeaders.Contains("X-Caller-Id"));
        Assert.False(client.DefaultRequestHeaders.Contains("X-Admin"));

        // Act
        using var response = await client.GetAsync(path, CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Verifies neither endpoint returns a JSON body describing the individual checks.</summary>
    /// <param name="path">The health endpoint path.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Get_Healthy_ReturnsNoJsonCheckDetails_Test(string path)
    {
        // Arrange
        using var factory = new ProductsApiFactory();
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync(path, CancellationToken.None);

        // Assert
        var text = await response.Content.ReadAsStringAsync(CancellationToken.None);
        Assert.NotEqual("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("{", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AppDbContext", text, StringComparison.Ordinal);
    }

    /// <summary>Verifies a configured path replaces the default.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetLive_ConfiguredPath_AnswersOnThatPath_Test()
    {
        // Arrange
        using var baseFactory = new ProductsApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.UseSetting("HealthEndpoints:LivePath", "/probe/alive")
        );
        using var client = factory.CreateClient();

        // Act
        using var custom = await client.GetAsync("/probe/alive", CancellationToken.None);
        using var original = await client.GetAsync("/health/live", CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, custom.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, original.StatusCode);
    }

    /// <summary>Verifies a malformed configured path stops the host from starting instead of failing later.</summary>
    /// <param name="key">The configuration key to corrupt.</param>
    /// <param name="value">The invalid value.</param>
    [Theory]
    [InlineData("HealthEndpoints:LivePath", "health/live")]
    [InlineData("HealthEndpoints:ReadyPath", "")]
    public void Start_InvalidHealthEndpointsOptions_FailsOptionsValidation_Test(
        string key,
        string value
    )
    {
        // Arrange
        using var baseFactory = new ProductsApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.UseSetting(key, value)
        );

        // Act
        var exception = Record.Exception(() => factory.CreateClient().Dispose());

        // Assert
        Assert.IsType<OptionsValidationException>(exception);
    }

    /// <summary>Makes every connection open fail once <see cref="Broken"/> is set, simulating an unreachable database.</summary>
    private sealed class DatabaseSwitch : DbConnectionInterceptor
    {
        /// <summary>Gets or sets a value indicating whether opening a connection should fail.</summary>
        public bool Broken { get; set; }

        /// <inheritdoc/>
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection,
            ConnectionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default
        ) =>
            Broken
                ? throw new InvalidOperationException("Database switched off for the test.")
                : ValueTask.FromResult(result);
    }
}
