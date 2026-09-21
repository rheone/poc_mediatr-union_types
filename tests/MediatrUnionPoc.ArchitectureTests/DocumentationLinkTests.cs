namespace MediatrUnionPoc.ArchitectureTests;

/// <summary>
/// Guards the documentation set against link rot: every relative link, every <c>#fragment</c>
/// (checked against the target file's GitHub heading anchors), every reference-style link and every
/// footnote in every Markdown file of the repository must resolve. Absolute URLs are not fetched.
/// The suite lives with the architecture tests because, like them, it enforces a structural rule
/// over the repository rather than the behaviour of a project.
/// </summary>
public sealed class DocumentationLinkTests : IDisposable
{
    // A fresh directory per instance: xUnit builds one instance per test and runs them in parallel,
    // so the scratch trees must not collide. The name never reaches an assertion.
    private readonly string _scratch = Path.Combine(
        Path.GetTempPath(),
        "doc-links-" + Guid.NewGuid().ToString("N")
    );

    /// <summary>Verifies every Markdown file in the repository has only resolvable links.</summary>
    [Fact]
    public void Repository_MarkdownLinks_AllResolve_Test()
    {
        // Arrange
        var root = RepositoryRoot();

        // Act
        var failures = MarkdownLinkChecker.Check(root);

        // Assert
        var report = string.Join(Environment.NewLine, failures);
        Assert.True(
            failures.Count == 0,
            $"{failures.Count} broken documentation link(s) (file:line -> target):{Environment.NewLine}{report}"
        );
    }

    /// <summary>Verifies the scan covers the root README and the docs, and skips the excluded trees.</summary>
    [Fact]
    public void Repository_MarkdownFiles_CoverDocsAndSkipExcludedTrees_Test()
    {
        // Arrange
        var root = RepositoryRoot();

        // Act
        var files = MarkdownLinkChecker.FindMarkdownFiles(root);

        // Assert
        Assert.Multiple(
            () => Assert.Contains("README.md", files),
            () => Assert.Contains("docs/index.md", files),
            () =>
                Assert.DoesNotContain(
                    files,
                    f => f.StartsWith(".claude/skills/", StringComparison.Ordinal)
                ),
            () =>
                Assert.DoesNotContain(
                    files,
                    f => f.StartsWith("docs/research/", StringComparison.Ordinal)
                ),
            () => Assert.DoesNotContain(files, f => f.Contains("/bin/", StringComparison.Ordinal))
        );
    }

    /// <summary>Verifies links that resolve, including to headings with duplicate anchors, produce no failure.</summary>
    [Fact]
    public void Check_ValidLinks_ReportsNothing_Test()
    {
        // Arrange
        Write(
            "a.md",
            "# Title\n\n## Options\n\ntext\n\n## Options\n\n[b](sub/b.md#deep) [dup](#options-1) [self](#title) [dir](sub/) [web](https://example.com/x#y)\n"
        );
        Write("sub/b.md", "# Deep\n");

        // Act
        var failures = MarkdownLinkChecker.Check(_scratch);

        // Assert
        Assert.Empty(failures);
    }

    /// <summary>Verifies a missing file, a missing fragment and a wrong-case path are each reported with file and line.</summary>
    [Fact]
    public void Check_BrokenLinks_ReportsFileLineAndTarget_Test()
    {
        // Arrange
        Write(
            "a.md",
            "# Title\n\n[gone](missing.md)\n[frag](b.md#nope)\n[case](B.md)\n[self](#nope-self)\n"
        );
        Write("b.md", "# Bee\n");

        // Act
        var failures = MarkdownLinkChecker.Check(_scratch, ["a.md"]);

        // Assert
        Assert.Multiple(
            () =>
                Assert.Contains(
                    failures,
                    f => f.StartsWith("a.md:3 -> missing.md", StringComparison.Ordinal)
                ),
            () =>
                Assert.Contains(
                    failures,
                    f => f.StartsWith("a.md:4 -> b.md#nope", StringComparison.Ordinal)
                ),
            () =>
                Assert.Contains(
                    failures,
                    f => f.StartsWith("a.md:5 -> B.md", StringComparison.Ordinal)
                ),
            () =>
                Assert.Contains(
                    failures,
                    f => f.StartsWith("a.md:6 -> #nope-self", StringComparison.Ordinal)
                )
        );
    }

    /// <summary>Verifies footnotes and reference links need a definition in the same file.</summary>
    [Fact]
    public void Check_FootnotesAndReferences_NeedDefinitionsInTheSameFile_Test()
    {
        // Arrange
        Write(
            "a.md",
            "# Title\n\nDefined[^ok] and undefined[^bad], [x][ref] and [y][nope].\n\n[^ok]: yes\n\n[ref]: b.md\n"
        );
        Write("b.md", "# Bee\n");

        // Act
        var failures = MarkdownLinkChecker.Check(_scratch, ["a.md"]);

        // Assert
        Assert.Multiple(
            () =>
                Assert.Contains(
                    failures,
                    f => f.StartsWith("a.md:3 -> [^bad]", StringComparison.Ordinal)
                ),
            () =>
                Assert.Contains(
                    failures,
                    f => f.StartsWith("a.md:3 -> [nope]", StringComparison.Ordinal)
                ),
            () => Assert.Equal(2, failures.Count)
        );
    }

    /// <summary>Verifies links inside fenced code blocks and inline code are ignored.</summary>
    [Fact]
    public void Check_LinksInCode_AreIgnored_Test()
    {
        // Arrange
        Write(
            "a.md",
            "# Title\n\n```md\n[x](nowhere.md) [^x]\n# not a heading\n```\n\nInline `[y](nowhere.md)` and `[^y]` too.\n\n[real](#title)\n"
        );

        // Act
        var failures = MarkdownLinkChecker.Check(_scratch);

        // Assert
        Assert.Empty(failures);
    }

    /// <summary>Verifies a fragment into a non-Markdown file is not checked.</summary>
    [Fact]
    public void Check_FragmentIntoNonMarkdownFile_IsIgnored_Test()
    {
        // Arrange
        Write("a.md", "# Title\n\n[code](code.cs#L10)\n");
        Write("code.cs", "class C {}\n");

        // Act
        var failures = MarkdownLinkChecker.Check(_scratch, ["a.md"]);

        // Assert
        Assert.Empty(failures);
    }

    /// <summary>Verifies the excluded directories are not scanned.</summary>
    [Fact]
    public void FindMarkdownFiles_ExcludedDirectories_AreSkipped_Test()
    {
        // Arrange
        Write("keep.md", "# Keep\n");
        Write("docs/research/quote.md", "# Quote\n");
        Write(".claude/skills/x/SKILL.md", "# Skill\n");
        Write("src/bin/Debug/out.md", "# Out\n");
        Write("node_modules/p/README.md", "# Pkg\n");
        Write("docs/page.md", "# Page\n");

        // Act
        var files = MarkdownLinkChecker.FindMarkdownFiles(_scratch);

        // Assert
        Assert.Equal(["docs/page.md", "keep.md"], files);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_scratch))
        {
            Directory.Delete(_scratch, recursive: true);
        }
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

    private void Write(string relativePath, string content)
    {
        var path = Path.Combine(_scratch, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
