using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace MediatrUnionPoc.Api.IntegrationTests.TestData;

/// <summary>What a contract comparison found different for one location in the document.</summary>
/// <param name="Kind">Whether the location exists only in the live document, only in the snapshot, or in both with different values.</param>
/// <param name="Path">A readable path to the location, for example <c>paths['/api/v1/products'].get.responses.504</c>.</param>
/// <param name="Snapshot">The snapshot's value (JSON text), or <see langword="null"/> when <see cref="Kind"/> is <see cref="OpenApiDifferenceKind.Added"/>.</param>
/// <param name="Live">The live document's value (JSON text), or <see langword="null"/> when <see cref="Kind"/> is <see cref="OpenApiDifferenceKind.Removed"/>.</param>
public sealed record OpenApiDifference(
    OpenApiDifferenceKind Kind,
    string Path,
    string? Snapshot,
    string? Live
);

/// <summary>How a location differs between the committed snapshot and the live document.</summary>
public enum OpenApiDifferenceKind
{
    /// <summary>The location exists only in the live document.</summary>
    Added,

    /// <summary>The location exists only in the committed snapshot.</summary>
    Removed,

    /// <summary>The location exists in both, with different values.</summary>
    Changed,
}

/// <summary>
/// The OpenAPI contract check: turns the served document into a deterministic, machine- and OS-independent
/// text, compares it with the committed snapshot and describes any difference in a form a reviewer can act on.
/// The snapshot is compared as a parsed tree, never as raw text, so line endings and key order in the file on
/// disk cannot cause a false alarm; the text written by <see cref="Serialize"/> is itself stable (sorted keys,
/// two-space indent, LF, one trailing newline).
/// </summary>
public static partial class OpenApiContract
{
    /// <summary>The environment variable that makes the contract test rewrite the snapshot instead of failing.</summary>
    public const string UpdateVariable = "UPDATE_OPENAPI_SNAPSHOT";

    /// <summary>The most differences a failure message lists.</summary>
    public const int MaximumListedDifferences = 25;

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        IndentSize = 2,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Produces the canonical form of a document: every object's members in ordinal key order (array order is
    /// kept, it is meaningful), every line ending inside a string turned into LF (documentation text carries the
    /// line endings of the source files it was compiled from, which differ between checkouts), and the <c>servers</c> member removed, because the address the test host
    /// answers on is a property of the machine and not of the contract.
    /// </summary>
    /// <param name="document">The parsed document; not modified.</param>
    /// <returns>A new tree.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    public static JsonNode Normalize(JsonNode document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var normalized = Sort(document)!;
        if (normalized is JsonObject root)
        {
            root.Remove("servers");
        }

        return normalized;
    }

    /// <summary>Writes a normalized document as the text kept in the snapshot file (LF line endings, one trailing newline).</summary>
    /// <param name="normalized">The document from <see cref="Normalize"/>.</param>
    /// <returns>The text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="normalized"/> is <see langword="null"/>.</exception>
    public static string Serialize(JsonNode normalized)
    {
        ArgumentNullException.ThrowIfNull(normalized);

        return normalized.ToJsonString(WriteOptions) + "\n";
    }

    /// <summary>Parses snapshot or document text and normalizes it.</summary>
    /// <param name="json">The JSON text (any line endings).</param>
    /// <returns>The normalized tree.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is <see langword="null"/>.</exception>
    public static JsonNode NormalizeText(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        return Normalize(JsonNode.Parse(json)!);
    }

    /// <summary>Compares the committed snapshot with the live document, location by location.</summary>
    /// <param name="snapshot">The normalized snapshot.</param>
    /// <param name="live">The normalized live document.</param>
    /// <returns>Every difference, ordered by path; empty when the contract is unchanged.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static IReadOnlyList<OpenApiDifference> Compare(JsonNode snapshot, JsonNode live)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(live);

        var expected = Flatten(snapshot);
        var actual = Flatten(live);
        List<OpenApiDifference> differences = [];

        foreach (var (path, value) in expected)
        {
            if (!actual.TryGetValue(path, out var current))
            {
                differences.Add(
                    new OpenApiDifference(OpenApiDifferenceKind.Removed, path, value, null)
                );
            }
            else if (!string.Equals(value, current, StringComparison.Ordinal))
            {
                differences.Add(
                    new OpenApiDifference(OpenApiDifferenceKind.Changed, path, value, current)
                );
            }
        }

        differences.AddRange(
            actual
                .Where(entry => !expected.ContainsKey(entry.Key))
                .Select(entry => new OpenApiDifference(
                    OpenApiDifferenceKind.Added,
                    entry.Key,
                    null,
                    entry.Value
                ))
        );

        return [.. differences.OrderBy(difference => difference.Path, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Builds the failure message: a headline, the operations added or removed, the first
    /// <see cref="MaximumListedDifferences"/> differences and the exact way to regenerate the snapshot.
    /// </summary>
    /// <param name="differences">The result of <see cref="Compare"/>; not empty.</param>
    /// <param name="snapshot">The normalized snapshot, for naming operations.</param>
    /// <param name="live">The normalized live document, for naming operations.</param>
    /// <param name="snapshotPath">The snapshot file's path as the repository spells it.</param>
    /// <returns>The message.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static string Describe(
        IReadOnlyList<OpenApiDifference> differences,
        JsonNode snapshot,
        JsonNode live,
        string snapshotPath
    )
    {
        ArgumentNullException.ThrowIfNull(differences);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(live);
        ArgumentNullException.ThrowIfNull(snapshotPath);

        var oldOperations = Operations(snapshot);
        var newOperations = Operations(live);
        var text = new StringBuilder();
        text.Append(
                "The OpenAPI contract served at /openapi/v1.json no longer matches the committed snapshot "
            )
            .Append(snapshotPath)
            .Append(": ")
            .Append(differences.Count(d => d.Kind == OpenApiDifferenceKind.Added))
            .Append(" added, ")
            .Append(differences.Count(d => d.Kind == OpenApiDifferenceKind.Removed))
            .Append(" removed, ")
            .Append(differences.Count(d => d.Kind == OpenApiDifferenceKind.Changed))
            .Append(" changed.\n");

        AppendList(
            text,
            "Operations added",
            newOperations.Except(oldOperations, StringComparer.Ordinal)
        );
        AppendList(
            text,
            "Operations removed",
            oldOperations.Except(newOperations, StringComparer.Ordinal)
        );

        text.Append("Differences (first ")
            .Append(Math.Min(MaximumListedDifferences, differences.Count))
            .Append(" of ")
            .Append(differences.Count)
            .Append("):\n");
        foreach (var difference in differences.Take(MaximumListedDifferences))
        {
            text.Append("  ")
                .Append(
                    difference.Kind switch
                    {
                        OpenApiDifferenceKind.Added =>
                            $"+ {difference.Path} = {Shorten(difference.Live)}",
                        OpenApiDifferenceKind.Removed =>
                            $"- {difference.Path} (was {Shorten(difference.Snapshot)})",
                        _ =>
                            $"~ {difference.Path}: {Shorten(difference.Snapshot)} -> {Shorten(difference.Live)}",
                    }
                )
                .Append('\n');
        }

        if (differences.Count > MaximumListedDifferences)
        {
            text.Append("  ... and ")
                .Append(differences.Count - MaximumListedDifferences)
                .Append(" more.\n");
        }

        text.Append(
                "If this change is intended, regenerate the snapshot, review its diff and commit it:\n"
            )
            .Append(
                "  bash:        UPDATE_OPENAPI_SNAPSHOT=1 dotnet test tests/MediatrUnionPoc.Api.IntegrationTests --filter \"FullyQualifiedName~OpenApiContractTests\"\n"
            )
            .Append(
                "  PowerShell:  $env:UPDATE_OPENAPI_SNAPSHOT=1; dotnet test tests/MediatrUnionPoc.Api.IntegrationTests --filter \"FullyQualifiedName~OpenApiContractTests\"; Remove-Item Env:UPDATE_OPENAPI_SNAPSHOT\n"
            )
            .Append("If it is not intended, the API change that caused it is the bug.");

        return text.ToString();
    }

    /// <summary>
    /// Decides what the contract test does. Rewriting the snapshot needs <see cref="UpdateVariable"/> set to
    /// <c>1</c> or <c>true</c>, and is refused when the <c>CI</c> variable is set, so a pipeline can never
    /// rewrite the contract it is meant to check.
    /// </summary>
    /// <param name="updateValue">The value of <see cref="UpdateVariable"/>, if any.</param>
    /// <param name="ciValue">The value of the <c>CI</c> variable, if any.</param>
    /// <returns>The mode.</returns>
    public static OpenApiContractMode ResolveMode(string? updateValue, string? ciValue)
    {
        var requested =
            string.Equals(updateValue, "1", StringComparison.Ordinal)
            || string.Equals(updateValue, "true", StringComparison.OrdinalIgnoreCase);

        if (!requested)
        {
            return OpenApiContractMode.Check;
        }

        return string.IsNullOrEmpty(ciValue)
            ? OpenApiContractMode.Update
            : OpenApiContractMode.UpdateRefusedInCi;
    }

    private static void AppendList(StringBuilder text, string heading, IEnumerable<string> items)
    {
        var list = items.Order(StringComparer.Ordinal).ToList();
        if (list.Count > 0)
        {
            text.Append(heading).Append(": ").Append(string.Join(", ", list)).Append('\n');
        }
    }

    private static List<string> Operations(JsonNode document) =>
        [
            .. (document["paths"] as JsonObject ?? [])
                .Where(path => path.Value is JsonObject)
                .SelectMany(path =>
                    path.Value!.AsObject()
                        .Select(method => $"{method.Key.ToUpperInvariant()} {path.Key}")
                ),
        ];

    private static string Shorten(string? value)
    {
        if (value is null)
        {
            return "(none)";
        }

        return value.Length <= 120 ? value : value[..117] + "...";
    }

    private static JsonNode? Sort(JsonNode? node) =>
        node switch
        {
            JsonObject obj => new JsonObject(
                obj.OrderBy(member => member.Key, StringComparer.Ordinal)
                    .Select(member => KeyValuePair.Create(member.Key, Sort(member.Value)))
            ),
            JsonArray array => new JsonArray(array.Select(Sort).ToArray()),
            null => null,
            JsonValue value when value.TryGetValue<string>(out var text) => JsonValue.Create(
                text.ReplaceLineEndings("\n")
            ),
            _ => node.DeepClone(),
        };

    private static Dictionary<string, string> Flatten(JsonNode root)
    {
        Dictionary<string, string> leaves = new(StringComparer.Ordinal);
        Walk(root, string.Empty, leaves);
        return leaves;
    }

    private static void Walk(JsonNode? node, string path, Dictionary<string, string> leaves)
    {
        switch (node)
        {
            case JsonObject { Count: > 0 } obj:
                foreach (var member in obj)
                {
                    Walk(member.Value, path + Segment(member.Key, path.Length == 0), leaves);
                }

                break;
            case JsonArray { Count: > 0 } array:
                for (var index = 0; index < array.Count; index++)
                {
                    Walk(array[index], $"{path}[{index}]", leaves);
                }

                break;
            default:
                leaves[path.Length == 0 ? "$" : path] = node?.ToJsonString(WriteOptions) ?? "null";
                break;
        }
    }

    private static string Segment(string key, bool first)
    {
        if (!PlainKey().IsMatch(key))
        {
            return $"['{key}']";
        }

        return first ? key : "." + key;
    }

    [GeneratedRegex("^[A-Za-z0-9_\\-]+$")]
    private static partial Regex PlainKey();
}

/// <summary>What the OpenAPI contract test does on this run.</summary>
public enum OpenApiContractMode
{
    /// <summary>Compare the live document with the snapshot and fail on any difference.</summary>
    Check,

    /// <summary>Rewrite the snapshot from the live document.</summary>
    Update,

    /// <summary>Rewriting was requested inside CI and is refused: the test fails.</summary>
    UpdateRefusedInCi,
}
