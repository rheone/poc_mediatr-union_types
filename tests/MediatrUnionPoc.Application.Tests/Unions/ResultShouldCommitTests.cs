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
    private const string ProductName = "Widget";
    private const decimal ProductPrice = 9.99m;
    private const string ErrorMessage = "boom";
    private const string ErrorCode = "BOOM";

    private static readonly ProductId SomeProductId = ProductId.From(
        Guid.Parse("77777777-7777-7777-7777-777777777777")
    );

    /// <summary>Rows: one per <see cref="CreateProductResult"/> case, with whether it should commit — only <see cref="ProductDto"/> does.</summary>
    public static TheoryData<
        CreateProductResult,
        bool
    > ShouldCommit_CreateProductResultCases_CommitsOnlyForProductDto_Test_Data =>
        new(
            CreateRow(
                "ProductDto",
                new ProductDto(SomeProductId, ProductName, ProductPrice, ProductVersion.Initial),
                true
            ),
            CreateRow(
                "ValidationErrors",
                new ValidationErrors([new ValidationError("Name", "required")]),
                false
            ),
            CreateRow("Error", new Error(ErrorMessage, ErrorCode), false)
        );

    /// <summary>Rows: one per <see cref="UpdateProductResult"/> case, with whether it should commit — only <see cref="ProductDto"/> does.</summary>
    public static TheoryData<
        UpdateProductResult,
        bool
    > ShouldCommit_UpdateProductResultCases_CommitsOnlyForSuccess_Test_Data =>
        new(
            UpdateRow(
                "ProductDto",
                new ProductDto(SomeProductId, ProductName, ProductPrice, ProductVersion.Initial),
                true
            ),
            UpdateRow("NotFound", new NotFound<ProductId>(SomeProductId), false),
            UpdateRow(
                "ValidationErrors",
                new ValidationErrors([new ValidationError("Price", "must be >= 0")]),
                false
            ),
            UpdateRow("Error", new Error(ErrorMessage, ErrorCode), false),
            UpdateRow("NotAuthorized", new NotAuthorized(["not the owner"]), false)
        );

    // Union values have no distinguishing ToString, so each row names its case explicitly to keep
    // display names unique.
    private static TheoryDataRow<CreateProductResult, bool> CreateRow(
        string caseName,
        CreateProductResult response,
        bool expected
    ) =>
        new(response, expected)
        {
            TestDisplayName =
                $"{nameof(ShouldCommit_CreateProductResultCases_CommitsOnlyForProductDto_Test)}(case: {caseName})",
        };

    private static TheoryDataRow<UpdateProductResult, bool> UpdateRow(
        string caseName,
        UpdateProductResult response,
        bool expected
    ) =>
        new(response, expected)
        {
            TestDisplayName =
                $"{nameof(ShouldCommit_UpdateProductResultCases_CommitsOnlyForSuccess_Test)}(case: {caseName})",
        };

    /// <summary>Verifies <see cref="CreateProductResult.ShouldCommit"/> commits only for its <see cref="ProductDto"/> case.</summary>
    /// <param name="response">The union instance to classify.</param>
    /// <param name="expected">Whether the case is expected to commit.</param>
    [Theory]
    [MemberData(nameof(ShouldCommit_CreateProductResultCases_CommitsOnlyForProductDto_Test_Data))]
    public void ShouldCommit_CreateProductResultCases_CommitsOnlyForProductDto_Test(
        CreateProductResult response,
        bool expected
    )
    {
        // Arrange (response supplied by ShouldCommit_CreateProductResultCases_CommitsOnlyForProductDto_Test_Data)

        // Act
        var shouldCommit = CreateProductResult.ShouldCommit(response);

        // Assert
        Assert.Equal(expected, shouldCommit);
    }

    /// <summary>Verifies <see cref="UpdateProductResult.ShouldCommit"/> commits only for its <see cref="Success"/> case.</summary>
    /// <param name="response">The union instance to classify.</param>
    /// <param name="expected">Whether the case is expected to commit.</param>
    [Theory]
    [MemberData(nameof(ShouldCommit_UpdateProductResultCases_CommitsOnlyForSuccess_Test_Data))]
    public void ShouldCommit_UpdateProductResultCases_CommitsOnlyForSuccess_Test(
        UpdateProductResult response,
        bool expected
    )
    {
        // Arrange (response supplied by ShouldCommit_UpdateProductResultCases_CommitsOnlyForSuccess_Test_Data)

        // Act
        var shouldCommit = UpdateProductResult.ShouldCommit(response);

        // Assert
        Assert.Equal(expected, shouldCommit);
    }
}
