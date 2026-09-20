using MediatrUnionPoc.Api.Audit;
using MediatrUnionPoc.Application.Common.Auditing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>Verifies the audit options: the default directory, validation on start (there is no switch to turn auditing off), and how a relative directory is resolved.</summary>
[Trait("Category", "Integration")]
public sealed class AuditOptionsTests : IDisposable
{
    private readonly ProductsApiFactory _factory = new();

    /// <summary>Disposes the test's backing <see cref="ProductsApiFactory"/>.</summary>
    public void Dispose() => _factory.Dispose();

    /// <summary>Verifies the default directory is <c>logs/audit</c> and the options carry no on/off switch.</summary>
    [Fact]
    public void Defaults_Directory_IsLogsAudit_Test()
    {
        // Act
        var options = new AuditOptions();

        // Assert
        Assert.Multiple(
            () => Assert.Equal("logs/audit", options.Directory),
            () =>
                Assert.DoesNotContain(
                    typeof(AuditOptions).GetProperties(),
                    p => p.PropertyType == typeof(bool)
                )
        );
    }

    /// <summary>Verifies an empty or blank directory fails options validation.</summary>
    /// <param name="directory">The invalid directory.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_BlankDirectory_Fails_Test(string? directory)
    {
        // Arrange
        var options = new AuditOptions { Directory = directory! };

        // Act
        var result = new AuditOptionsValidator().Validate(null, options);

        // Assert
        Assert.True(result.Failed);
    }

    /// <summary>Verifies a host with a blank audit directory refuses to start.</summary>
    [Fact]
    public void Start_BlankDirectory_FailsOptionsValidation_Test()
    {
        // Arrange
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Audit:Directory", string.Empty)
        );

        // Act
        var exception = Record.Exception(() => factory.CreateClient().Dispose());

        // Assert
        Assert.IsType<OptionsValidationException>(exception);
    }

    /// <summary>Verifies the configured directory is the one the host writes to, and is created on demand.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Host_ConfiguredDirectory_IsTheOneWrittenTo_Test()
    {
        // Arrange
        var log = _factory.Services.GetRequiredService<IAuditLog>();

        // Act
        await log.RecordAsync(
            new AuditEvent
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTimeOffset.UtcNow,
                Action = "Test.Probe",
                Outcome = "ok",
            },
            CancellationToken.None
        );

        // Assert
        Assert.Single(_factory.ReadAuditEvents());
    }

    /// <summary>Verifies a relative directory is resolved against the content root, not the working directory.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task AddAudit_RelativeDirectory_IsResolvedAgainstTheContentRoot_Test()
    {
        // Arrange
        var contentRoot = Path.Combine(
            Path.GetTempPath(),
            "mediatr-union-poc-audit-tests",
            "content-root-" + Guid.NewGuid().ToString("N")
        );
        try
        {
            var services = new ServiceCollection();
            services.AddSingleton<IHostEnvironment>(new StubEnvironment(contentRoot));
            services.AddSingleton<IConfiguration>(
                new ConfigurationBuilder()
                    .AddInMemoryCollection(
                        new Dictionary<string, string?> { ["Audit:Directory"] = "rel/audit" }
                    )
                    .Build()
            );
            services.AddAudit();
            await using var provider = services.BuildServiceProvider();

            // Act
            await provider
                .GetRequiredService<IAuditLog>()
                .RecordAsync(
                    new AuditEvent
                    {
                        Id = Guid.NewGuid(),
                        Timestamp = DateTimeOffset.UtcNow,
                        Action = "Test.Probe",
                        Outcome = "ok",
                    },
                    CancellationToken.None
                );

            // Assert
            Assert.Single(
                ProductsApiFactory.ReadAuditEvents(Path.Combine(contentRoot, "rel", "audit"))
            );
        }
        finally
        {
            if (Directory.Exists(contentRoot))
            {
                Directory.Delete(contentRoot, recursive: true);
            }
        }
    }

    private sealed class StubEnvironment(string contentRoot) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";

        public string ApplicationName { get; set; } = "Test";

        public string ContentRootPath { get; set; } = contentRoot;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
