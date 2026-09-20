using System.Text.RegularExpressions;

namespace MediatrUnionPoc.Api.IntegrationTests.TestData;

/// <summary>
/// Reads one top-level section of <c>docs/operations.md</c>, the page that documents the defaults of
/// the CORS, rate-limiting and request-timeout options, so a test can check the documented defaults
/// against only the section it is about (the page holds several options tables).
/// </summary>
public static partial class OperationsDocumentation
{
    private const string PageRelativePath = "docs/operations.md";

    /// <summary>Reads the text of one <c>#</c> section, from its heading line to the next <c>#</c> heading (fenced code is skipped).</summary>
    /// <param name="repositoryRoot">The repository root.</param>
    /// <param name="headingStart">The start of the section's heading text, such as <c>CORS</c>.</param>
    /// <returns>The heading line and everything under it, up to the next top-level heading.</returns>
    public static string Section(string repositoryRoot, string headingStart)
    {
        var lines = File.ReadAllLines(
            Path.Combine(repositoryRoot, PageRelativePath.Replace('/', Path.DirectorySeparatorChar))
        );
        var section = new List<string>();
        var inFence = false;
        var collecting = false;
        foreach (var line in lines)
        {
            if (FenceLine().IsMatch(line))
            {
                inFence = !inFence;
            }

            var isTopLevelHeading = !inFence && line.StartsWith("# ", StringComparison.Ordinal);
            if (isTopLevelHeading)
            {
                if (collecting)
                {
                    break;
                }

                collecting = line.StartsWith("# " + headingStart, StringComparison.Ordinal);
            }

            if (collecting)
            {
                section.Add(line);
            }
        }

        Assert.True(
            section.Count > 0,
            $"{PageRelativePath} has no top-level section starting with \"# {headingStart}\"."
        );
        return string.Join('\n', section);
    }

    [GeneratedRegex(@"^\s*(```|~~~)", RegexOptions.None, 1000)]
    private static partial Regex FenceLine();
}
