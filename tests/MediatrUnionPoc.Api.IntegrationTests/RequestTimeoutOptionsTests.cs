using System.Text.RegularExpressions;
using MediatrUnionPoc.Api.IntegrationTests.TestData;
using MediatrUnionPoc.Api.RequestTimeouts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>Verifies the request-timeout options: secure defaults (the same in code, in appsettings.json and in the documentation), every validation rule, and that an invalid value stops the host from starting.</summary>
[Trait("Category", "Integration")]
public sealed class RequestTimeoutOptionsTests : IDisposable
{
    private readonly ProductsApiFactory _factory = new();

    /// <inheritdoc/>
    public void Dispose() => _factory.Dispose();

    /// <summary>Verifies the defaults are bounded, the impersonation timeout is the shorter one, and the defaults validate.</summary>
    [Fact]
    public void Defaults_AreBoundedAndOrdered_Test()
    {
        // Arrange
        var expectedDefault = TimeSpan.FromSeconds(30);
        var expectedImpersonation = TimeSpan.FromSeconds(10);

        // Act
        var options = new RequestTimeoutOptions();

        // Assert
        Assert.Multiple(
            () => Assert.Equal(expectedDefault, options.Default),
            () => Assert.Equal(expectedImpersonation, options.Impersonation),
            () => Assert.True(options.Impersonation < options.Default),
            () => Assert.True(Validate(options).Succeeded)
        );
    }

    /// <summary>Verifies appsettings.json ships exactly the defaults the class carries.</summary>
    [Fact]
    public void AppSettings_MatchTheDefaultsInCode_Test()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(
                Path.Combine(RepositoryRoot(), "src", "MediatrUnionPoc.Api", "appsettings.json")
            )
            .Build();

        // Act
        var bound = configuration
            .GetSection(RequestTimeoutOptions.SectionName)
            .Get<RequestTimeoutOptions>()!;
        var defaults = new RequestTimeoutOptions();

        // Assert
        Assert.Equivalent(defaults, bound);
    }

    /// <summary>Verifies the request-timeout section of docs/operations.md lists every setting with its default.</summary>
    [Fact]
    public void OperationsDocs_DocumentTheTimeoutDefaults_Test()
    {
        // Arrange
        var section = OperationsDocumentation.Section(RepositoryRoot(), "Request timeouts");
        var defaultRow = new Regex(@"\|\s*`RequestTimeouts:Default`\s*\|\s*`00:00:30`\s*\|");
        var impersonationRow = new Regex(
            @"\|\s*`RequestTimeouts:Impersonation`\s*\|\s*`00:00:10`\s*\|"
        );

        // Act
        var defaultMatches = defaultRow.IsMatch(section);
        var impersonationMatches = impersonationRow.IsMatch(section);

        // Assert
        Assert.Multiple(
            () => Assert.True(defaultMatches, "The Default row is missing or wrong."),
            () => Assert.True(impersonationMatches, "The Impersonation row is missing or wrong.")
        );
    }

    /// <summary>Verifies each timeout is bounded to one millisecond through ten minutes, for both settings.</summary>
    /// <param name="setting">The setting under test.</param>
    /// <param name="value">The timeout, as a time span string.</param>
    /// <param name="valid">Whether it is valid.</param>
    [Theory]
    [InlineData("Default", "00:00:00", false)]
    [InlineData("Default", "-00:00:05", false)]
    [InlineData("Default", "00:10:00.001", false)]
    [InlineData("Default", "1.00:00:00", false)]
    [InlineData("Default", "00:00:00.001", true)]
    [InlineData("Default", "00:10:00", true)]
    [InlineData("Impersonation", "00:00:00", false)]
    [InlineData("Impersonation", "00:10:01", false)]
    [InlineData("Impersonation", "00:00:00.050", true)]
    [InlineData("Impersonation", "00:10:00", true)]
    public void Validate_Timeout_IsBounded_Test(string setting, string value, bool valid)
    {
        // Arrange
        var span = TimeSpan.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        var options = new RequestTimeoutOptions();
        if (setting == "Default")
        {
            options.Default = span;
        }
        else
        {
            options.Impersonation = span;
        }

        // Act
        var result = Validate(options);

        // Assert
        Assert.Equal(valid, result.Succeeded);
    }

    /// <summary>Verifies a host configured with an invalid timeout refuses to start, for each setting.</summary>
    /// <param name="setting">The setting under test.</param>
    [Theory]
    [InlineData("Default")]
    [InlineData("Impersonation")]
    public void Start_InvalidTimeout_FailsOptionsValidation_Test(string setting)
    {
        // Arrange
        using var factory = _factory.WithTimeouts(configure: builder =>
            builder.UseSetting($"RequestTimeouts:{setting}", "00:00:00")
        );

        // Act
        var exception = Record.Exception(() => factory.CreateClient().Dispose());

        // Assert
        var validation = Assert.IsType<OptionsValidationException>(exception);
        Assert.NotEmpty(validation.Failures);
    }

    private static ValidateOptionsResult Validate(RequestTimeoutOptions options) =>
        new RequestTimeoutOptionsValidator().Validate(null, options);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (
            directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "MediatrUnionPoc.slnx"))
        )
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
