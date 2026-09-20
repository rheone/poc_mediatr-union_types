using System.Text.RegularExpressions;

namespace MediatrUnionPoc.ArchitectureTests;

/// <summary>
/// Finds every Markdown file of a repository and reports the relative links, link fragments,
/// reference-style links and footnote references that do not resolve. Absolute URLs are never
/// fetched, and links inside fenced code blocks and inline code are ignored.
/// </summary>
internal static partial class MarkdownLinkChecker
{
    private static readonly string[] IgnoredDirectoryNames =
    [
        ".git",
        ".vs",
        ".scratch",
        "bin",
        "obj",
        "node_modules",
    ];

    private static readonly string[] IgnoredPathPrefixes = [".claude/skills/", "docs/research/"];

    /// <summary>
    /// Enumerates the Markdown files under a root, skipping build output, dependencies, vendored
    /// skills and the research notes that quote external text.
    /// </summary>
    /// <param name="root">The repository root.</param>
    /// <returns>Repository-relative paths, always separated by <c>/</c>, sorted ordinally.</returns>
    public static IReadOnlyList<string> FindMarkdownFiles(string root)
    {
        var found = new List<string>();
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var entry in Directory.EnumerateDirectories(directory))
            {
                var name = Path.GetFileName(entry);
                var relative = Relative(root, entry) + "/";
                if (
                    !IgnoredDirectoryNames.Contains(name, StringComparer.Ordinal)
                    && !IgnoredPathPrefixes.Any(prefix =>
                        relative.StartsWith(prefix, StringComparison.Ordinal)
                    )
                )
                {
                    pending.Push(entry);
                }
            }

            foreach (var file in Directory.EnumerateFiles(directory, "*.md"))
            {
                found.Add(Relative(root, file));
            }
        }

        found.Sort(StringComparer.Ordinal);
        return found;
    }

    /// <summary>Checks every Markdown file under a root.</summary>
    /// <param name="root">The repository root.</param>
    /// <returns>One <c>file:line -&gt; target (reason)</c> entry per broken link; empty when all resolve.</returns>
    public static IReadOnlyList<string> Check(string root) => Check(root, FindMarkdownFiles(root));

    /// <summary>Checks the given Markdown files.</summary>
    /// <param name="root">The repository root.</param>
    /// <param name="files">Repository-relative paths of the files to check.</param>
    /// <returns>One <c>file:line -&gt; target (reason)</c> entry per broken link; empty when all resolve.</returns>
    public static IReadOnlyList<string> Check(string root, IEnumerable<string> files)
    {
        var scans = new Dictionary<string, ScannedFile>(StringComparer.Ordinal);
        var listings = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var failures = new List<string>();

        ScannedFile ScanOf(string fullPath)
        {
            if (!scans.TryGetValue(fullPath, out var scan))
            {
                scan = Scan(File.ReadAllLines(fullPath));
                scans[fullPath] = scan;
            }

            return scan;
        }

        foreach (var file in files)
        {
            var fullFile = Path.GetFullPath(Path.Combine(root, file));
            var scan = ScanOf(fullFile);

            foreach (
                var use in scan.FootnoteUses.Where(u =>
                    !scan.FootnoteDefinitions.Contains(u.Target)
                )
            )
            {
                failures.Add(
                    $"{file}:{use.Line} -> [^{use.Target}] (footnote has no definition in this file)"
                );
            }

            foreach (
                var use in scan.ReferenceUses.Where(u =>
                    !scan.ReferenceDefinitions.Contains(Normalize(u.Target))
                )
            )
            {
                failures.Add(
                    $"{file}:{use.Line} -> [{use.Target}] (reference link has no definition in this file)"
                );
            }

            foreach (var link in scan.Links)
            {
                var problem = Validate(root, fullFile, link.Target, ScanOf, listings);
                if (problem is not null)
                {
                    failures.Add($"{file}:{link.Line} -> {link.Target} ({problem})");
                }
            }
        }

        return failures;
    }

    /// <summary>Parses the lines of one Markdown file into its headings, definitions and links.</summary>
    /// <param name="lines">The lines of the file.</param>
    /// <returns>What the file declares and what it links to.</returns>
    internal static ScannedFile Scan(IReadOnlyList<string> lines)
    {
        var headings = new List<string>();
        var links = new List<Use>();
        var referenceUses = new List<Use>();
        var footnoteUses = new List<Use>();
        var referenceDefinitions = new HashSet<string>(StringComparer.Ordinal);
        var footnoteDefinitions = new HashSet<string>(StringComparer.Ordinal);
        var fence = '\0';
        var fenceLength = 0;

        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var number = index + 1;

            var fenceMatch = Fence().Match(line);
            if (fenceMatch.Success)
            {
                var marker = fenceMatch.Groups["marker"].Value;
                if (fence == '\0')
                {
                    fence = marker[0];
                    fenceLength = marker.Length;
                }
                else if (
                    marker[0] == fence
                    && marker.Length >= fenceLength
                    && string.IsNullOrWhiteSpace(fenceMatch.Groups["rest"].Value)
                )
                {
                    fence = '\0';
                }

                continue;
            }

            if (fence != '\0')
            {
                continue;
            }

            var heading = Heading().Match(line);
            if (heading.Success)
            {
                headings.Add(heading.Groups["text"].Value);
            }

            var masked = CodeSpan().Replace(line, match => new string(' ', match.Length));

            var footnoteDefinition = FootnoteDefinition().Match(masked);
            if (footnoteDefinition.Success)
            {
                footnoteDefinitions.Add(footnoteDefinition.Groups["label"].Value);
            }

            var referenceDefinition = ReferenceDefinition().Match(masked);
            if (referenceDefinition.Success)
            {
                referenceDefinitions.Add(Normalize(referenceDefinition.Groups["label"].Value));
                links.Add(
                    new Use(number, referenceDefinition.Groups["target"].Value.Trim('<', '>'))
                );
            }

            foreach (var match in InlineLink().Matches(masked).Cast<Match>())
            {
                var target = match.Groups["angled"].Success
                    ? match.Groups["angled"].Value
                    : match.Groups["plain"].Value;
                links.Add(new Use(number, target));
            }

            foreach (var match in ReferenceUse().Matches(masked).Cast<Match>())
            {
                referenceUses.Add(new Use(number, match.Groups["label"].Value));
            }

            foreach (var match in FootnoteReference().Matches(masked).Cast<Match>())
            {
                var isDefinition = match.Index == 0 && masked.AsSpan(match.Length).StartsWith(":");
                if (!isDefinition)
                {
                    footnoteUses.Add(new Use(number, match.Groups["label"].Value));
                }
            }
        }

        return new ScannedFile(
            MarkdownSlug.ForHeadings(headings),
            links,
            referenceUses,
            footnoteUses,
            referenceDefinitions,
            footnoteDefinitions
        );
    }

    private static string? Validate(
        string root,
        string fullFile,
        string target,
        Func<string, ScannedFile> scanOf,
        Dictionary<string, HashSet<string>> listings
    )
    {
        if (target.Length == 0 || SchemePrefix().IsMatch(target))
        {
            return null;
        }

        var hash = target.IndexOf('#', StringComparison.Ordinal);
        var pathPart = hash < 0 ? target : target[..hash];
        var fragment = hash < 0 ? string.Empty : Uri.UnescapeDataString(target[(hash + 1)..]);
        var query = pathPart.IndexOf('?', StringComparison.Ordinal);
        if (query >= 0)
        {
            pathPart = pathPart[..query];
        }

        string resolved;
        if (pathPart.Length == 0)
        {
            resolved = fullFile;
        }
        else
        {
            var decoded = Uri.UnescapeDataString(pathPart);
            resolved = Path.GetFullPath(
                decoded.StartsWith('/')
                    ? Path.Combine(root, decoded.TrimStart('/'))
                    : Path.Combine(Path.GetDirectoryName(fullFile)!, decoded)
            );
            if (!ExistsExactly(root, resolved, listings))
            {
                return "no such file or directory";
            }
        }

        if (
            fragment.Length > 0
            && File.Exists(resolved)
            && resolved.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            && !scanOf(resolved).Slugs.Contains(fragment, StringComparer.Ordinal)
        )
        {
            return $"no heading with anchor #{fragment} in {Relative(root, resolved)}";
        }

        return null;
    }

    /// <summary>
    /// Checks a path exists with exactly the letter case the link spells, so a link that only works
    /// on a case-insensitive file system (Windows, macOS) is still reported.
    /// </summary>
    private static bool ExistsExactly(
        string root,
        string fullPath,
        Dictionary<string, HashSet<string>> listings
    )
    {
        var relative = Path.GetRelativePath(root, fullPath);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            return false;
        }

        var current = root;
        foreach (var segment in relative.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
            {
                continue;
            }

            if (!Directory.Exists(current))
            {
                return false;
            }

            if (!listings.TryGetValue(current, out var names))
            {
                names = Directory
                    .EnumerateFileSystemEntries(current)
                    .Select(entry => Path.GetFileName(entry))
                    .ToHashSet(StringComparer.Ordinal);
                listings[current] = names;
            }

            if (!names.Contains(segment))
            {
                return false;
            }

            current = Path.Combine(current, segment);
        }

        return true;
    }

    private static string Relative(string root, string fullPath) =>
        Path.GetRelativePath(root, fullPath).Replace('\\', '/');

    private static string Normalize(string label) =>
        WhiteSpace().Replace(label.Trim(), " ").ToLowerInvariant();

    /// <summary>A link target, reference label or footnote label at a 1-based line.</summary>
    /// <param name="Line">The 1-based line number.</param>
    /// <param name="Target">The link target, or the reference or footnote label.</param>
    internal sealed record Use(int Line, string Target);

    /// <summary>What one Markdown file declares and what it links to.</summary>
    /// <param name="Slugs">The anchor of every heading, with duplicate suffixes.</param>
    /// <param name="Links">Inline and definition link targets.</param>
    /// <param name="ReferenceUses">Uses of <c>[text][label]</c>.</param>
    /// <param name="FootnoteUses">Uses of <c>[^label]</c>.</param>
    /// <param name="ReferenceDefinitions">Normalized labels defined by <c>[label]: url</c>.</param>
    /// <param name="FootnoteDefinitions">Labels defined by <c>[^label]: text</c>.</param>
    internal sealed record ScannedFile(
        IReadOnlyList<string> Slugs,
        IReadOnlyList<Use> Links,
        IReadOnlyList<Use> ReferenceUses,
        IReadOnlyList<Use> FootnoteUses,
        HashSet<string> ReferenceDefinitions,
        HashSet<string> FootnoteDefinitions
    );

    [GeneratedRegex(@"^ {0,3}(?<marker>`{3,}|~{3,})(?<rest>.*)$", RegexOptions.None, 1000)]
    private static partial Regex Fence();

    [GeneratedRegex(
        @"^ {0,3}#{1,6}[ \t]+(?<text>.*?)(?:[ \t]+#+)?[ \t]*$",
        RegexOptions.None,
        1000
    )]
    private static partial Regex Heading();

    [GeneratedRegex(@"(?<ticks>`+)(?<code>.+?)\k<ticks>", RegexOptions.None, 1000)]
    private static partial Regex CodeSpan();

    [GeneratedRegex(@"^\[\^(?<label>[^\]\s]+)\]:", RegexOptions.None, 1000)]
    private static partial Regex FootnoteDefinition();

    [GeneratedRegex(
        @"^ {0,3}\[(?<label>[^\]^][^\]]*)\]:[ \t]*(?<target>\S+)",
        RegexOptions.None,
        1000
    )]
    private static partial Regex ReferenceDefinition();

    [GeneratedRegex(
        @"\]\(\s*(?:<(?<angled>[^>]*)>|(?<plain>(?:[^()\s]|\([^()\s]*\))*))",
        RegexOptions.None,
        1000
    )]
    private static partial Regex InlineLink();

    [GeneratedRegex(@"\]\[(?<label>[^\]^][^\]]*)\]", RegexOptions.None, 1000)]
    private static partial Regex ReferenceUse();

    [GeneratedRegex(@"\[\^(?<label>[^\]\s]+)\]", RegexOptions.None, 1000)]
    private static partial Regex FootnoteReference();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9+.\-]*:", RegexOptions.None, 1000)]
    private static partial Regex SchemePrefix();

    [GeneratedRegex(@"\s+", RegexOptions.None, 1000)]
    private static partial Regex WhiteSpace();
}
