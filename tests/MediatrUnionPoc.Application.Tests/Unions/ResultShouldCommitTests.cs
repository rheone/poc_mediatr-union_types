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
    /// <summary>Verifies <see cref="CreateProductResult.ShouldCommit"/> commits only for its <see cref="ProductDto"/> case.</summary>
    [Fact]
    public void CreateProductResult_commits_only_for_the_ProductDto_case()
    {
        CreateProductResult dto = new ProductDto(ProductId.New(), "Widget", 9.99m);
        CreateProductResult validationErrors = new ValidationErrors([
            new ValidationError("Name", "required"),
        ]);
        CreateProductResult error = new Error("boom", "BOOM");

        Assert.True(CreateProductResult.ShouldCommit(dto));
        Assert.False(CreateProductResult.ShouldCommit(validationErrors));
        Assert.False(CreateProductResult.ShouldCommit(error));
    }

    /// <summary>Verifies <see cref="UpdateProductResult.ShouldCommit"/> commits only for its <see cref="Success"/> case.</summary>
    [Fact]
    public void UpdateProductResult_commits_only_for_the_Success_case()
    {
        UpdateProductResult success = new Success();
        UpdateProductResult notFound = new NotFound<ProductId>(ProductId.New());
        UpdateProductResult validationErrors = new ValidationErrors([
            new ValidationError("Price", "must be >= 0"),
        ]);
        UpdateProductResult error = new Error("boom", "BOOM");

        Assert.True(UpdateProductResult.ShouldCommit(success));
        Assert.False(UpdateProductResult.ShouldCommit(notFound));
        Assert.False(UpdateProductResult.ShouldCommit(validationErrors));
        Assert.False(UpdateProductResult.ShouldCommit(error));
    }
}
