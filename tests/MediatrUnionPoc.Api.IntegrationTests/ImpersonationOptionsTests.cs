using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using static MediatrUnionPoc.Api.IntegrationTests.TestData.JwtTestTokens;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>Verifies the impersonation options are validated on start: a host that could mint tokens under a missing, weak or shared key, or with contradictory lifetimes, refuses to start.</summary>
[Trait("Category", "Integration")]
public sealed class ImpersonationOptionsTests : IDisposable
{
    private readonly ProductsApiFactory _factory = new(ApiAuthentication.RealJwt);

    /// <summary>Disposes the test's backing <see cref="ProductsApiFactory"/>.</summary>
    public void Dispose() => _factory.Dispose();

    /// <summary>Verifies a non-Development host with impersonation enabled but no key of its own refuses to start, naming the setting.</summary>
    [Fact]
    public void Start_NonDevelopmentEnabledWithoutKey_FailsOptionsValidation_Test()
    {
        // Arrange
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder
                .UseEnvironment("Production")
                .UseSetting("Authentication:Jwt:SigningKey", NonDevelopmentSigningKey)
        );

        // Act
        var exception = Record.Exception(() => factory.CreateClient().Dispose());

        // Assert
        var validation = Assert.IsType<OptionsValidationException>(exception);
        Assert.Contains(
            validation.Failures,
            failure => failure.Contains("Impersonation:SigningKey", StringComparison.Ordinal)
        );
    }

    /// <summary>Verifies a non-Development host with impersonation switched off starts without a key.</summary>
    [Fact]
    public void Start_NonDevelopmentDisabledWithoutKey_Starts_Test()
    {
        // Arrange
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder
                .UseEnvironment("Production")
                .UseSetting("Authentication:Jwt:SigningKey", NonDevelopmentSigningKey)
                .UseSetting("Impersonation:Enabled", "false")
        );

        // Act
        var exception = Record.Exception(() => factory.CreateClient().Dispose());

        // Assert
        Assert.Null(exception);
    }

    /// <summary>Verifies a key that is too short, or lifetimes that are non-positive, above the ceiling or contradict each other, stop the host starting.</summary>
    /// <param name="key">The configuration key to set.</param>
    /// <param name="value">The invalid value.</param>
    [Theory]
    [InlineData("Impersonation:SigningKey", "too-short")]
    [InlineData("Impersonation:DefaultLifetimeMinutes", "0")]
    [InlineData("Impersonation:MaxLifetimeMinutes", "5000")]
    [InlineData("Impersonation:DefaultLifetimeMinutes", "90")]
    public void Start_InvalidImpersonationOptions_FailsOptionsValidation_Test(
        string key,
        string value
    )
    {
        // Arrange
        using var factory = _factory.WithWebHostBuilder(builder => builder.UseSetting(key, value));

        // Act
        var exception = Record.Exception(() => factory.CreateClient().Dispose());

        // Assert
        Assert.IsType<OptionsValidationException>(exception);
    }

    /// <summary>Verifies the impersonation key may not be the ordinary token key.</summary>
    [Fact]
    public void Start_ImpersonationKeySameAsOrdinaryKey_FailsOptionsValidation_Test()
    {
        // Arrange
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder
                .UseSetting("Authentication:Jwt:SigningKey", NonDevelopmentSigningKey)
                .UseSetting("Impersonation:SigningKey", NonDevelopmentSigningKey)
        );

        // Act
        var exception = Record.Exception(() => factory.CreateClient().Dispose());

        // Assert
        var validation = Assert.IsType<OptionsValidationException>(exception);
        Assert.Contains(
            validation.Failures,
            failure => failure.Contains("must differ", StringComparison.Ordinal)
        );
    }
}
