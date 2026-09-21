using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MediatrUnionPoc.Api.Authentication;
using MediatrUnionPoc.Api.Impersonation;
using MediatrUnionPoc.Api.IntegrationTests.TestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>
/// The OpenAPI contract check. The document the Development host serves at <c>/openapi/v1.json</c> is
/// compared, after normalization, with the snapshot committed under <c>Contracts/</c>, so a change to the
/// public contract (an added or removed operation, response or schema member) fails the build until the
/// snapshot is deliberately regenerated (see <see cref="OpenApiContract.UpdateVariable"/>) and reviewed in
/// the same commit.
/// </summary>
[Trait("Category", "Integration")]
public sealed class OpenApiContractTests : IDisposable
{
    private const string SnapshotRelativePath =
        "tests/MediatrUnionPoc.Api.IntegrationTests/Contracts/openapi.v1.json";

    private readonly ProductsApiFactory _factory = new();

    /// <inheritdoc/>
    public void Dispose() => _factory.Dispose();

    /// <summary>
    /// Verifies the live document equals the committed snapshot. With <c>UPDATE_OPENAPI_SNAPSHOT=1</c> (and no
    /// <c>CI</c> variable) it rewrites the snapshot instead; the rewritten text is what the next run compares
    /// against, so running twice proves the file is stable.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task LiveDocument_MatchesTheCommittedSnapshot_Test()
    {
        // Arrange
        var live = await FetchNormalizedAsync();
        var path = Path.Combine(RepositoryRoot(), SnapshotRelativePath);
        var mode = OpenApiContract.ResolveMode(
            Environment.GetEnvironmentVariable(OpenApiContract.UpdateVariable),
            Environment.GetEnvironmentVariable("CI")
        );

        // Act / Assert
        switch (mode)
        {
            case OpenApiContractMode.UpdateRefusedInCi:
                Assert.Fail(
                    $"{OpenApiContract.UpdateVariable} is set but the CI variable is too: a pipeline never rewrites the contract it checks. Regenerate the snapshot locally and commit it."
                );
                break;
            case OpenApiContractMode.Update:
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(
                    path,
                    OpenApiContract.Serialize(live),
                    new UTF8Encoding(false),
                    CancellationToken.None
                );
                break;
            default:
                await AssertMatchesAsync(path, live);
                break;
        }
    }

    /// <summary>Verifies the served document lists versioned paths only: the transitional unversioned alias never appears in the contract.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task LiveDocument_ListsVersionedPathsOnly_Test()
    {
        // Arrange / Act
        var live = await FetchNormalizedAsync();

        // Assert
        var paths = live["paths"]!.AsObject().Select(path => path.Key).ToList();
        Assert.Multiple(
            () => Assert.NotEmpty(paths),
            () =>
                Assert.All(
                    paths,
                    path => Assert.StartsWith("/api/v1/", path, StringComparison.Ordinal)
                )
        );
    }

    /// <summary>Verifies neither the served document nor the committed snapshot contains a signing key, a bearer token or a local path.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task DocumentAndSnapshot_ContainNoSecretsOrMachineDetails_Test()
    {
        // Arrange
        var live = OpenApiContract.Serialize(await FetchNormalizedAsync());
        var snapshotPath = Path.Combine(RepositoryRoot(), SnapshotRelativePath);
        var snapshot = File.Exists(snapshotPath)
            ? await File.ReadAllTextAsync(snapshotPath, CancellationToken.None)
            : string.Empty;
        var services = _factory.Services;
        var forbidden = new[]
        {
            services.GetRequiredService<IOptions<JwtAuthOptions>>().Value.SigningKey,
            services.GetRequiredService<IOptions<ImpersonationOptions>>().Value.SigningKey,
            "DEVELOPMENT-ONLY",
            _factory.AuditDirectory,
            RepositoryRoot(),
        };

        // Act / Assert
        Assert.DoesNotMatch(new Regex(@"eyJ[\w-]+\.[\w-]+\.[\w-]+"), live + snapshot);
        Assert.Multiple(
            forbidden
                .Where(value => !string.IsNullOrEmpty(value))
                .SelectMany(value =>
                    new (string Name, string Text)[]
                    {
                        ("live", live),
                        ("snapshot", snapshot),
                    }.Select(document =>
                        (Action)(
                            () =>
                                Assert.DoesNotContain(
                                    value,
                                    document.Text,
                                    StringComparison.OrdinalIgnoreCase
                                )
                        )
                    )
                )
                .ToArray()
        );
    }

    /// <summary>Verifies the snapshot on disk is already in canonical form (so rewriting it changes nothing) and that the same normalization of the live document is identical between two fetches.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Normalization_IsStableAcrossFetchesAndTheSnapshotIsCanonical_Test()
    {
        // Arrange
        var first = OpenApiContract.Serialize(await FetchNormalizedAsync());
        var second = OpenApiContract.Serialize(await FetchNormalizedAsync());
        var snapshotPath = Path.Combine(RepositoryRoot(), SnapshotRelativePath);

        // Act
        var snapshotText = await File.ReadAllTextAsync(snapshotPath, CancellationToken.None);
        var canonical = OpenApiContract.Serialize(OpenApiContract.NormalizeText(snapshotText));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(first, second),
            () => Assert.DoesNotContain('\r', snapshotText),
            () => Assert.EndsWith("\n", snapshotText, StringComparison.Ordinal),
            () => Assert.Equal(canonical, snapshotText)
        );
    }

    private static async Task AssertMatchesAsync(string path, JsonNode live)
    {
        if (!File.Exists(path))
        {
            Assert.Fail(
                $"The OpenAPI snapshot {SnapshotRelativePath} does not exist. Create it with {OpenApiContract.UpdateVariable}=1 (see the \"OpenAPI contract check\" section of docs/operations.md) and commit it."
            );
        }

        var snapshot = OpenApiContract.NormalizeText(
            await File.ReadAllTextAsync(path, CancellationToken.None)
        );
        var differences = OpenApiContract.Compare(snapshot, live);
        if (differences.Count > 0)
        {
            Assert.Fail(
                OpenApiContract.Describe(differences, snapshot, live, SnapshotRelativePath)
            );
        }
    }

    private async Task<JsonNode> FetchNormalizedAsync()
    {
        using var client = _factory.CreateClient();
        var text = await client.GetStringAsync(ApiRoutes.OpenApiV1, CancellationToken.None);

        return OpenApiContract.NormalizeText(text);
    }

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
