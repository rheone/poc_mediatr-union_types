using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Features.Products.Common;
using MediatrUnionPoc.Application.Features.Products.Create;
using MediatrUnionPoc.Application.Features.Products.Update;
using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Application.Tests.Unions;

/// <summary>
/// Verifies <see cref="CreateProductResult.ShouldCommit"/> and
/// <see cref="UpdateProductResult.ShouldCommit"/> classify every one of their own declared cases
/// correctly. <c>TransactionBehaviorTests</c> already proves <c>TransactionBehavior</c> asks the
/// union rather than inspecting case types itself, using <c>DeleteProductResult</c> as its
/// example — these tests cover the two result unions that example doesn't touch, since a mistake
/// in either <c>ShouldCommit</c> switch is exactly the kind of per-union authoring error the
/// pattern is meant to isolate.
/// </summary>
public class ResultShouldCommitTests
{
    private static readonly ProductId SomeProductId = ProductId.From(
        Guid.Parse("77777777-7777-7777-7777-777777777777")
    );

    /// <summary>Rows: one per <see cref="CreateProductResult"/> case, with whether it should commit — only <see cref="ProductDto"/> does.</summary>
    public static TheoryData<
        CreateProductResult,
        bool
    > CreateProductResult_ShouldCommit_Test_Data =>
        new()
        {
            { new ProductDto(SomeProductId, "Widget", 9.99m), true },
            { new ValidationErrors([new ValidationError("Name", "required")]), false },
            { new Error("boom", "BOOM"), false },
        };

    /// <summary>Rows: one per <see cref="UpdateProductResult"/> case, with whether it should commit — only <see cref="Success"/> does.</summary>
    public static TheoryData<
        UpdateProductResult,
        bool
    > UpdateProductResult_ShouldCommit_Test_Data =>
        new()
        {
            { new Success(), true },
            { new NotFound<ProductId>(SomeProductId), false },
            { new ValidationErrors([new ValidationError("Price", "must be >= 0")]), false },
            { new Error("boom", "BOOM"), false },
            { new NotAuthorized(["not the owner"]), false },
        };

    /// <summary>Verifies <see cref="CreateProductResult.ShouldCommit"/> commits only for its <see cref="ProductDto"/> case.</summary>
    /// <param name="response">The union instance to classify.</param>
    /// <param name="expected">Whether the case is expected to commit.</param>
    [Theory]
    [MemberData(nameof(CreateProductResult_ShouldCommit_Test_Data))]
    public void CreateProductResult_ShouldCommit_commits_only_for_the_ProductDto_case(
        CreateProductResult response,
        bool expected
    )
    {
        // Arrange (response supplied by CreateProductResult_ShouldCommit_Test_Data)

        // Act
        var shouldCommit = CreateProductResult.ShouldCommit(response);

        // Assert
        Assert.Equal(expected, shouldCommit);
    }

    /// <summary>Verifies <see cref="UpdateProductResult.ShouldCommit"/> commits only for its <see cref="Success"/> case.</summary>
    /// <param name="response">The union instance to classify.</param>
    /// <param name="expected">Whether the case is expected to commit.</param>
    [Theory]
    [MemberData(nameof(UpdateProductResult_ShouldCommit_Test_Data))]
    public void UpdateProductResult_ShouldCommit_commits_only_for_the_Success_case(
        UpdateProductResult response,
        bool expected
    )
    {
        // Arrange (response supplied by UpdateProductResult_ShouldCommit_Test_Data)

        // Act
        var shouldCommit = UpdateProductResult.ShouldCommit(response);

        // Assert
        Assert.Equal(expected, shouldCommit);
    }
}
