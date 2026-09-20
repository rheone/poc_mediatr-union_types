namespace MediatrUnionPoc.ArchitectureTests;

/// <summary>Verifies <see cref="MarkdownSlug"/> reproduces GitHub's heading anchors on the tricky headings this repo uses.</summary>
public sealed class MarkdownSlugTests
{
    /// <summary>Verifies the slug of headings with backticks, generics, colons, quotes, dashes and plus signs.</summary>
    /// <param name="heading">The heading text.</param>
    /// <param name="expected">The anchor GitHub generates.</param>
    [Theory]
    [InlineData("The C# `union` type", "the-c-union-type")]
    [InlineData("C# language concepts", "c-language-concepts")]
    [InlineData(
        "Static abstract interface members: why generic code can build a union it's never seen",
        "static-abstract-interface-members-why-generic-code-can-build-a-union-its-never-seen"
    )]
    [InlineData(
        "Role-based: `IRequiresAuthorization` + `AuthorizationBehavior`",
        "role-based-irequiresauthorization--authorizationbehavior"
    )]
    [InlineData(
        "Resource-based: `ResourceAuthorizationService` + `OwnerAuthorizationHandler<TResource>`",
        "resource-based-resourceauthorizationservice--ownerauthorizationhandlertresource"
    )]
    [InlineData(
        "A commit can fail too: `ICommitFailable`",
        "a-commit-can-fail-too-icommitfailable"
    )]
    [InlineData(
        "Optimistic concurrency: `ProductVersion`, `ETag` and `If-Match`",
        "optimistic-concurrency-productversion-etag-and-if-match"
    )]
    [InlineData("Commit vs. rollback, message by message", "commit-vs-rollback-message-by-message")]
    [InlineData(
        "Partial updates: `PATCH` as JSON Merge Patch",
        "partial-updates-patch-as-json-merge-patch"
    )]
    [InlineData(
        "Why not `IAuthorizationRequirementData` attributes",
        "why-not-iauthorizationrequirementdata-attributes"
    )]
    [InlineData("Warning: `AllowCredentials`", "warning-allowcredentials")]
    [InlineData("The `429` response", "the-429-response")]
    [InlineData(
        "Worked example: `UpdateProductCommand`, case by case",
        "worked-example-updateproductcommand-case-by-case"
    )]
    [InlineData(
        "Zero-to-many handlers, and multiple requirements",
        "zero-to-many-handlers-and-multiple-requirements"
    )]
    [InlineData(
        "Shared [case types](#x) are **meaning-free**",
        "shared-case-types-are-meaning-free"
    )]
    [InlineData("A raw <b>tag</b> and `Optional<T>` code", "a-raw-tag-and-optionalt-code")]
    public void ForHeading_TrickyHeadings_MatchGitHub_Test(string heading, string expected) =>
        Assert.Equal(expected, MarkdownSlug.ForHeading(heading));

    /// <summary>Verifies repeated headings get numeric suffixes in document order.</summary>
    [Fact]
    public void ForHeadings_DuplicateHeadings_GetNumericSuffixes_Test()
    {
        // Arrange
        string[] headings =
        [
            "Options",
            "Where it sits in the pipeline",
            "Options",
            "Options",
            "Other",
        ];

        // Act
        var slugs = MarkdownSlug.ForHeadings(headings);

        // Assert
        Assert.Equal(
            ["options", "where-it-sits-in-the-pipeline", "options-1", "options-2", "other"],
            slugs
        );
    }
}
