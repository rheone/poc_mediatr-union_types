using System.Runtime.CompilerServices;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Features.Products.Delete;
using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Application.Tests.Unions;

/// <summary>
/// Verifies <see cref="DeleteProductResult.ShouldCommit"/> classifies every one of its declared
/// cases correctly, including <see cref="NotAuthorized"/>, and that
/// <see cref="DeleteProductResult.FromNotAuthorized"/> round-trips into the union's
/// <see cref="NotAuthorized"/> case.
/// </summary>
public class DeleteProductResultTests
{
    private static readonly ProductId SomeProductId = ProductId.From(
        Guid.Parse("66666666-6666-6666-6666-666666666666")
    );

    /// <summary>Rows: one per declared case, with whether that case should commit — only <see cref="Success"/> does.</summary>
    public static TheoryData<DeleteProductResult, bool> ShouldCommit_Test_Data =>
        new()
        {
            { new Success(), true },
            { new NotFound<ProductId>(SomeProductId), false },
            { new Error("boom", "BOOM"), false },
            { new NotAuthorized(["not an administrator"]), false },
        };

    /// <summary>Verifies <see cref="DeleteProductResult.ShouldCommit"/> commits only for its <see cref="Success"/> case.</summary>
    /// <param name="response">The union instance to classify.</param>
    /// <param name="expected">Whether the case is expected to commit.</param>
    [Theory]
    [MemberData(nameof(ShouldCommit_Test_Data))]
    public void ShouldCommit_commits_only_for_the_Success_case(
        DeleteProductResult response,
        bool expected
    )
    {
        // Arrange (response supplied by ShouldCommit_Test_Data)

        // Act
        var shouldCommit = DeleteProductResult.ShouldCommit(response);

        // Assert
        Assert.Equal(expected, shouldCommit);
    }

    /// <summary>Verifies <see cref="DeleteProductResult.FromNotAuthorized"/> builds a union instance carrying the given <see cref="NotAuthorized"/>.</summary>
    [Fact]
    public void FromNotAuthorized_carries_the_given_reasons()
    {
        // Arrange
        var notAuthorized = new NotAuthorized(["not an administrator"]);

        // Act
        DeleteProductResult result = DeleteProductResult.FromNotAuthorized(notAuthorized);

        // Assert
        var reasons = Assert.IsType<NotAuthorized>(((IUnion)result).Value).Reasons;
        Assert.Equal(["not an administrator"], reasons);
    }
}
