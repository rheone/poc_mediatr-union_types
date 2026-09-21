using System.Text.Json.Nodes;
using MediatrUnionPoc.Api.IntegrationTests.TestData;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>Proves the contract comparer detects the changes it exists for, against a mutated copy of a real document, and that its normalization ignores what is not part of the contract.</summary>
public sealed class OpenApiContractComparerTests
{
    private const string ProductPath = "/api/v1/products/{id}";

    /// <summary>Verifies a document compared with itself has no differences, whatever the key order or line endings of the text it was read from.</summary>
    [Fact]
    public void Compare_SameDocumentInAnotherTextForm_HasNoDifferences_Test()
    {
        // Arrange
        var snapshot = Baseline();
        var reordered = OpenApiContract.NormalizeText(
            """{"servers":[{"url":"http://localhost/"}],"paths":{},"openapi":"3.1.1","info":{"version":"1","title":"t\r\nx"}}"""
        );
        var other = OpenApiContract.NormalizeText(
            "{\n  \"openapi\": \"3.1.1\",\r\n  \"info\": { \"title\": \"t\\nx\", \"version\": \"1\" }, \"paths\": {}\n}"
        );

        // Act
        var same = OpenApiContract.Compare(
            snapshot,
            OpenApiContract.NormalizeText(OpenApiContract.Serialize(snapshot))
        );
        var forms = OpenApiContract.Compare(reordered, other);

        // Assert
        Assert.Multiple(() => Assert.Empty(same), () => Assert.Empty(forms));
    }

    /// <summary>Verifies an added path, a removed response code and a changed schema property type are each reported, with the right kind and a path that names the location.</summary>
    [Fact]
    public void Compare_AddedPathRemovedResponseChangedProperty_ReportsEachOne_Test()
    {
        // Arrange
        var snapshot = Baseline();
        var live = OpenApiContract.NormalizeText(OpenApiContract.Serialize(snapshot));
        live["paths"]!.AsObject()["/api/v1/widgets"] = new JsonObject
        {
            ["get"] = new JsonObject
            {
                ["responses"] = new JsonObject
                {
                    ["200"] = new JsonObject { ["description"] = "OK" },
                },
            },
        };
        live["paths"]![ProductPath]!["get"]!["responses"]!.AsObject().Remove("404");
        var (schema, property) = FirstSchemaProperty(live);
        live["components"]!["schemas"]![schema]!["properties"]![property]!["type"] = "boolean";

        // Act
        var differences = OpenApiContract.Compare(snapshot, live);

        // Assert
        Assert.Multiple(
            () =>
                Assert.Contains(
                    differences,
                    d =>
                        d.Kind == OpenApiDifferenceKind.Added
                        && d.Path.StartsWith("paths['/api/v1/widgets']", StringComparison.Ordinal)
                ),
            () =>
                Assert.Contains(
                    differences,
                    d =>
                        d.Kind == OpenApiDifferenceKind.Removed
                        && d.Path.StartsWith(
                            $"paths['{ProductPath}'].get.responses.404",
                            StringComparison.Ordinal
                        )
                ),
            () =>
                Assert.Contains(
                    differences,
                    d =>
                        d.Kind == OpenApiDifferenceKind.Changed
                        && d.Path == $"components.schemas.{schema}.properties.{property}.type"
                        && d.Live == "\"boolean\""
                )
        );
    }

    /// <summary>Verifies the failure message names the operations added and removed, lists differences, caps the list, and gives the exact regeneration command.</summary>
    [Fact]
    public void Describe_ManyDifferences_IsActionableAndBounded_Test()
    {
        // Arrange
        var snapshot = Baseline();
        var live = OpenApiContract.NormalizeText(OpenApiContract.Serialize(snapshot));
        live["paths"]!.AsObject()["/api/v1/widgets"] = new JsonObject
        {
            ["get"] = new JsonObject { ["operationId"] = "listWidgets" },
        };
        live["paths"]!.AsObject().Remove("/api/v1/impersonation/tokens");
        var differences = OpenApiContract.Compare(snapshot, live);

        // Act
        var message = OpenApiContract.Describe(differences, snapshot, live, "some/path.json");

        // Assert
        Assert.Multiple(
            () => Assert.True(differences.Count > OpenApiContract.MaximumListedDifferences),
            () =>
                Assert.Contains(
                    "Operations added: GET /api/v1/widgets",
                    message,
                    StringComparison.Ordinal
                ),
            () =>
                Assert.Contains(
                    "Operations removed: POST /api/v1/impersonation/tokens",
                    message,
                    StringComparison.Ordinal
                ),
            () =>
                Assert.Contains(
                    "- paths['/api/v1/impersonation/tokens'].post",
                    message,
                    StringComparison.Ordinal
                ),
            () =>
                Assert.Contains(
                    $"and {differences.Count - OpenApiContract.MaximumListedDifferences} more",
                    message,
                    StringComparison.Ordinal
                ),
            () =>
                Assert.Contains(
                    "UPDATE_OPENAPI_SNAPSHOT=1 dotnet test",
                    message,
                    StringComparison.Ordinal
                ),
            () => Assert.Contains("some/path.json", message, StringComparison.Ordinal)
        );
    }

    /// <summary>Verifies rewriting the snapshot needs an explicit 1 or true and is refused whenever the CI variable is set.</summary>
    /// <param name="update">The value of the update variable.</param>
    /// <param name="ci">The value of the CI variable.</param>
    /// <param name="expected">The resulting mode.</param>
    [Theory]
    [InlineData(null, null, OpenApiContractMode.Check)]
    [InlineData("", null, OpenApiContractMode.Check)]
    [InlineData("0", null, OpenApiContractMode.Check)]
    [InlineData("yes", null, OpenApiContractMode.Check)]
    [InlineData("1", null, OpenApiContractMode.Update)]
    [InlineData("true", "", OpenApiContractMode.Update)]
    [InlineData("1", "true", OpenApiContractMode.UpdateRefusedInCi)]
    [InlineData(null, "true", OpenApiContractMode.Check)]
    public void ResolveMode_UpdateNeedsAnExplicitRequestAndNeverRunsInCi_Test(
        string? update,
        string? ci,
        OpenApiContractMode expected
    )
    {
        // Arrange (the parameters are the inputs)

        // Act
        var mode = OpenApiContract.ResolveMode(update, ci);

        // Assert
        Assert.Equal(expected, mode);
    }

    private static JsonNode Baseline()
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
        return OpenApiContract.NormalizeText(
            File.ReadAllText(
                Path.Combine(
                    directory.FullName,
                    "tests",
                    "MediatrUnionPoc.Api.IntegrationTests",
                    "Contracts",
                    "openapi.v1.json"
                )
            )
        );
    }

    private static (string Schema, string Property) FirstSchemaProperty(JsonNode document)
    {
        foreach (var schema in document["components"]!["schemas"]!.AsObject())
        {
            if (schema.Value?["properties"] is JsonObject { Count: > 0 } properties)
            {
                foreach (var property in properties)
                {
                    if (property.Value?["type"] is JsonValue)
                    {
                        return (schema.Key, property.Key);
                    }
                }
            }
        }

        throw new InvalidOperationException(
            "The baseline has no schema property with a simple type."
        );
    }
}
