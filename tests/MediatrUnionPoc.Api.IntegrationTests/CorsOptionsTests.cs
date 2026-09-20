using MediatrUnionPoc.Api.Cors;
using MediatrUnionPoc.Api.IntegrationTests.TestData;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>Verifies the CORS options: secure defaults, the single-sourced header contract, and every validation rule (each also fails host start).</summary>
[Trait("Category", "Integration")]
public sealed class CorsOptionsTests : IDisposable
{
    private readonly ProductsApiFactory _factory = new();

    /// <inheritdoc/>
    public void Dispose() => _factory.Dispose();

    /// <summary>Verifies the defaults: no origin, the documented methods, headers and exposed headers, credentials off.</summary>
    [Fact]
    public void Defaults_AreSecureAndComplete_Test()
    {
        // Act
        var options = new ApiCorsOptions();

        // Assert
        Assert.Multiple(
            () => Assert.Empty(options.AllowedOrigins),
            () =>
                Assert.Equal(
                    ["GET", "POST", "PUT", "PATCH", "DELETE"],
                    options.GetAllowedMethods()
                ),
            () =>
                Assert.Equal(
                    ["Authorization", "Content-Type", "If-Match", "Accept"],
                    options.GetAllowedHeaders()
                ),
            () =>
                Assert.Equal(
                    [
                        "ETag",
                        "Link",
                        "X-Total-Count",
                        "X-Trace-Id",
                        "Location",
                        "Retry-After",
                        "api-supported-versions",
                    ],
                    options.GetExposedHeaders()
                ),
            () => Assert.False(options.AllowCredentials),
            () => Assert.Equal(600, options.PreflightMaxAgeSeconds),
            () => Assert.True(Validate(options).Succeeded)
        );
    }

    /// <summary>Verifies the CORS section of docs/operations.md documents every default list entry (the docs table derives from the same defaults).</summary>
    [Fact]
    public void OperationsDocs_DocumentEveryDefault_Test()
    {
        // Arrange
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (
            directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "MediatrUnionPoc.slnx"))
        )
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        var section = OperationsDocumentation.Section(directory.FullName, "CORS");

        // Act
        var missing = ApiCorsOptions
            .DefaultAllowedMethods.Concat(ApiCorsOptions.DefaultAllowedHeaders)
            .Concat(ApiCorsOptions.DefaultExposedHeaders)
            .Where(entry => !section.Contains($"`{entry}`", StringComparison.Ordinal))
            .ToList();

        // Assert
        Assert.Empty(missing);
    }

    /// <summary>Verifies a canonical origin, with or without a port, passes.</summary>
    /// <param name="origin">A valid origin.</param>
    [Theory]
    [InlineData("https://app.example.com")]
    [InlineData("http://localhost:5173")]
    [InlineData("https://app.example.com:8443")]
    public void Validate_CanonicalOrigin_Passes_Test(string origin) =>
        Assert.True(Validate(new ApiCorsOptions { AllowedOrigins = [origin] }).Succeeded);

    /// <summary>Verifies an origin that is not a plain http/https origin fails.</summary>
    /// <param name="origin">The invalid origin.</param>
    [Theory]
    [InlineData("*")]
    [InlineData("https://*.example.com")]
    [InlineData("")]
    [InlineData("app.example.com")]
    [InlineData("ftp://app.example.com")]
    [InlineData("https://app.example.com/")]
    [InlineData("https://app.example.com/path")]
    [InlineData("https://app.example.com?x=1")]
    [InlineData("https://app.example.com#frag")]
    [InlineData("https://user@app.example.com")]
    [InlineData("https://APP.example.com")]
    [InlineData("https://app.example.com:443")]
    public void Validate_InvalidOrigin_Fails_Test(string origin) =>
        Assert.True(Validate(new ApiCorsOptions { AllowedOrigins = [origin] }).Failed);

    /// <summary>Verifies the wildcard origin is rejected outright, with a message saying so.</summary>
    [Fact]
    public void Validate_WildcardOrigin_FailsWithAnExplicitMessage_Test()
    {
        // Act
        var result = Validate(new ApiCorsOptions { AllowedOrigins = ["*"] });

        // Assert
        Assert.Contains("wildcard", string.Join(' ', result.Failures!), StringComparison.Ordinal);
    }

    /// <summary>Verifies a duplicate origin fails.</summary>
    [Fact]
    public void Validate_DuplicateOrigin_Fails_Test() =>
        Assert.True(
            Validate(
                new ApiCorsOptions
                {
                    AllowedOrigins = ["https://a.example.com", "https://a.example.com"],
                }
            ).Failed
        );

    /// <summary>Verifies empty, whitespace, separator, wildcard and duplicate method entries fail.</summary>
    /// <param name="first">The first method.</param>
    /// <param name="second">The second method.</param>
    [Theory]
    [InlineData("", "GET")]
    [InlineData("G ET", "POST")]
    [InlineData("GET,POST", "PUT")]
    [InlineData("*", "GET")]
    [InlineData("GET", "get")]
    public void Validate_InvalidMethods_Fail_Test(string first, string second) =>
        Assert.True(Validate(new ApiCorsOptions { AllowedMethods = [first, second] }).Failed);

    /// <summary>Verifies an empty method list fails, since it would allow nothing at all.</summary>
    [Fact]
    public void Validate_EmptyMethodList_Fails_Test() =>
        Assert.True(Validate(new ApiCorsOptions { AllowedMethods = [] }).Failed);

    /// <summary>Verifies empty, malformed, wildcard and duplicate header entries fail in both header lists.</summary>
    /// <param name="first">The first header.</param>
    /// <param name="second">The second header.</param>
    [Theory]
    [InlineData("", "Accept")]
    [InlineData("X Bad", "Accept")]
    [InlineData("X-Bad:", "Accept")]
    [InlineData("*", "Accept")]
    [InlineData("Accept", "ACCEPT")]
    public void Validate_InvalidHeaders_Fail_Test(string first, string second)
    {
        Assert.True(Validate(new ApiCorsOptions { AllowedHeaders = [first, second] }).Failed);
        Assert.True(Validate(new ApiCorsOptions { ExposedHeaders = [first, second] }).Failed);
    }

    /// <summary>Verifies an empty allowed-header list fails while an empty exposed-header list (expose nothing) is valid.</summary>
    [Fact]
    public void Validate_EmptyHeaderLists_AllowedFailsExposedPasses_Test() =>
        Assert.Multiple(
            () => Assert.True(Validate(new ApiCorsOptions { AllowedHeaders = [] }).Failed),
            () => Assert.True(Validate(new ApiCorsOptions { ExposedHeaders = [] }).Succeeded)
        );

    /// <summary>Verifies a preflight max-age outside its range fails and the bounds pass.</summary>
    /// <param name="seconds">The max-age.</param>
    /// <param name="valid">Whether it is valid.</param>
    [Theory]
    [InlineData(-1, false)]
    [InlineData(86401, false)]
    [InlineData(0, true)]
    [InlineData(86400, true)]
    public void Validate_PreflightMaxAge_IsBounded_Test(int seconds, bool valid) =>
        Assert.Equal(
            valid,
            Validate(new ApiCorsOptions { PreflightMaxAgeSeconds = seconds }).Succeeded
        );

    /// <summary>Verifies a host with a wildcard origin refuses to start.</summary>
    [Fact]
    public void Start_WildcardOrigin_FailsOptionsValidation_Test()
    {
        // Arrange
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.PostConfigure<ApiCorsOptions>(o => o.AllowedOrigins = ["*"])
            )
        );

        // Act
        var exception = Record.Exception(() => factory.CreateClient().Dispose());

        // Assert
        Assert.IsType<OptionsValidationException>(exception);
    }

    /// <summary>Verifies a host with an out-of-range max-age refuses to start.</summary>
    [Fact]
    public void Start_MaxAgeOutOfRange_FailsOptionsValidation_Test()
    {
        // Arrange
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Cors:PreflightMaxAgeSeconds", "999999")
        );

        // Act
        var exception = Record.Exception(() => factory.CreateClient().Dispose());

        // Assert
        Assert.IsType<OptionsValidationException>(exception);
    }

    private static ValidateOptionsResult Validate(ApiCorsOptions options) =>
        new ApiCorsOptionsValidator().Validate(null, options);
}
