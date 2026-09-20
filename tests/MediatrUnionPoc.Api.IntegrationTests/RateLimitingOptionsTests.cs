using System.Text.RegularExpressions;
using MediatrUnionPoc.Api.IntegrationTests.TestData;
using MediatrUnionPoc.Api.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>Verifies the rate-limiting options: secure defaults (the same in code, in appsettings.json and in the documentation), every validation rule (each also fails host start), and what a live configuration reload does and does not reach.</summary>
[Trait("Category", "Integration")]
public sealed class RateLimitingOptionsTests : IDisposable
{
    private readonly ProductsApiFactory _factory = new();

    /// <inheritdoc/>
    public void Dispose() => _factory.Dispose();

    /// <summary>Verifies the defaults are bounded, reads are the loosest budget and minting the tightest, nothing queues, and the defaults validate.</summary>
    [Fact]
    public void Defaults_AreBoundedAndOrdered_Test()
    {
        // Act
        var options = new RateLimitingOptions();

        // Assert
        Assert.Multiple(
            () => Assert.True(options.Reads.PermitLimit > options.Writes.PermitLimit),
            () => Assert.True(options.Writes.PermitLimit > options.Impersonation.PermitLimit),
            () => Assert.Equal(0, options.Reads.QueueLimit),
            () => Assert.Equal(0, options.Writes.QueueLimit),
            () => Assert.Equal(0, options.Impersonation.QueueLimit),
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
            .GetSection(RateLimitingOptions.SectionName)
            .Get<RateLimitingOptions>()!;
        var defaults = new RateLimitingOptions();

        // Assert
        Assert.Multiple(
            () => Assert.Equivalent(defaults.Reads, bound.Reads),
            () => Assert.Equivalent(defaults.Writes, bound.Writes),
            () => Assert.Equivalent(defaults.Impersonation, bound.Impersonation)
        );
    }

    /// <summary>Verifies the rate-limiting section of docs/operations.md lists every policy with its default limit, window and queue.</summary>
    [Fact]
    public void OperationsDocs_DocumentTheRateLimitDefaults_Test()
    {
        // Arrange
        var section = OperationsDocumentation.Section(RepositoryRoot(), "Rate limiting");
        var defaults = new RateLimitingOptions();

        // Act
        var missing = new (string Policy, RateLimitPolicyOptions Options)[]
        {
            ("Reads", defaults.Reads),
            ("Writes", defaults.Writes),
            ("Impersonation", defaults.Impersonation),
        }
            .Where(p =>
                !Regex.IsMatch(
                    section,
                    $@"\|\s*`{p.Policy}`\s*\|\s*{p.Options.PermitLimit}\s*\|\s*{p.Options.WindowSeconds}\s*\|\s*{p.Options.QueueLimit}\s*\|"
                )
            )
            .Select(p => p.Policy)
            .ToList();

        // Assert
        Assert.Empty(missing);
    }

    /// <summary>Verifies each policy's permit limit is bounded to 1 through the maximum, for every policy.</summary>
    /// <param name="policy">The policy under test.</param>
    /// <param name="limit">The permit limit.</param>
    /// <param name="valid">Whether it is valid.</param>
    [Theory]
    [InlineData("Reads", 0, false)]
    [InlineData("Reads", -1, false)]
    [InlineData("Reads", 1_000_001, false)]
    [InlineData("Reads", 1, true)]
    [InlineData("Reads", 1_000_000, true)]
    [InlineData("Writes", 0, false)]
    [InlineData("Writes", 1_000_001, false)]
    [InlineData("Impersonation", 0, false)]
    [InlineData("Impersonation", 1_000_001, false)]
    [InlineData("Impersonation", 1, true)]
    public void Validate_PermitLimit_IsBounded_Test(string policy, int limit, bool valid) =>
        Assert.Equal(valid, Validate(With(policy, o => o.PermitLimit = limit)).Succeeded);

    /// <summary>Verifies each policy's window is 1 second to a day.</summary>
    /// <param name="policy">The policy under test.</param>
    /// <param name="seconds">The window.</param>
    /// <param name="valid">Whether it is valid.</param>
    [Theory]
    [InlineData("Reads", 0, false)]
    [InlineData("Reads", 86_401, false)]
    [InlineData("Reads", 1, true)]
    [InlineData("Reads", 86_400, true)]
    [InlineData("Writes", 0, false)]
    [InlineData("Impersonation", 86_401, false)]
    public void Validate_Window_IsBounded_Test(string policy, int seconds, bool valid) =>
        Assert.Equal(valid, Validate(With(policy, o => o.WindowSeconds = seconds)).Succeeded);

    /// <summary>Verifies each policy's queue is 0 to the maximum, and that a negative or huge queue fails.</summary>
    /// <param name="policy">The policy under test.</param>
    /// <param name="queue">The queue limit.</param>
    /// <param name="valid">Whether it is valid.</param>
    [Theory]
    [InlineData("Reads", -1, false)]
    [InlineData("Reads", 1_001, false)]
    [InlineData("Reads", 0, true)]
    [InlineData("Writes", 1_000, true)]
    [InlineData("Impersonation", -1, false)]
    public void Validate_QueueLimit_IsBounded_Test(string policy, int queue, bool valid) =>
        Assert.Equal(valid, Validate(With(policy, o => o.QueueLimit = queue)).Succeeded);

    /// <summary>Verifies a missing policy object fails validation instead of throwing later.</summary>
    [Fact]
    public void Validate_MissingPolicy_Fails_Test() =>
        Assert.True(Validate(new RateLimitingOptions { Writes = null! }).Failed);

    /// <summary>Verifies a host configured with an invalid limit refuses to start.</summary>
    [Fact]
    public void Start_InvalidLimit_FailsOptionsValidation_Test()
    {
        // Arrange
        using var factory = _factory.WithLimits(configure: builder =>
            builder.UseSetting("RateLimiting:Writes:PermitLimit", "0")
        );

        // Act
        var exception = Record.Exception(() => factory.CreateClient().Dispose());

        // Assert
        Assert.IsType<OptionsValidationException>(exception);
    }

    /// <summary>
    /// Verifies the limits are read once, at start: a configuration reload changes nothing for a new caller,
    /// and after a reload that carries an invalid value the limiter keeps working with the limits it started
    /// with instead of failing every later caller (a restart would refuse to start on the value instead).
    /// The reload itself may throw on the invalid value, as it does for every validated options class.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Reload_ChangedOrInvalidLimit_IsIgnoredUntilRestart_Test()
    {
        // Arrange
        using var factory = _factory.WithLimits(reads: 2);
        using var alice = factory.CreateClient().AsUser("alice");
        using var bob = factory.CreateClient().AsUser("bob");
        using var carol = factory.CreateClient().AsUser("carol");
        var services = factory.Services;
        var configuration = (IConfigurationRoot)services.GetRequiredService<IConfiguration>();
        var aliceBefore = await alice.GetStatusesAsync(ApiRoutes.Products, 3);

        // Act
        configuration["RateLimiting:Reads:PermitLimit"] = "4";
        var raised = Record.Exception(() => configuration.Reload());
        var bobAfterRaise = await bob.GetStatusesAsync(ApiRoutes.Products, 3);
        configuration["RateLimiting:Reads:PermitLimit"] = "0";
        _ = Record.Exception(() => configuration.Reload());
        var carolAfterInvalid = await carol.GetStatusesAsync(ApiRoutes.Products, 3);

        // Assert
        Assert.Multiple(
            () => Assert.Equal([200, 200, 429], aliceBefore),
            () => Assert.Null(raised),
            () => Assert.Equal([200, 200, 429], bobAfterRaise),
            () => Assert.Equal([200, 200, 429], carolAfterInvalid)
        );
    }

    private static RateLimitingOptions With(string policy, Action<RateLimitPolicyOptions> change)
    {
        var options = new RateLimitingOptions();
        change(
            policy switch
            {
                "Reads" => options.Reads,
                "Writes" => options.Writes,
                _ => options.Impersonation,
            }
        );

        return options;
    }

    private static ValidateOptionsResult Validate(RateLimitingOptions options) =>
        new RateLimitingOptionsValidator().Validate(null, options);

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
