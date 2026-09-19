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
    /// <summary>Verifies <see cref="DeleteProductResult.ShouldCommit"/> commits only for its <see cref="Success"/> case.</summary>
    [Fact]
    public void ShouldCommit_commits_only_for_the_Success_case()
    {
        DeleteProductResult success = new Success();
        DeleteProductResult notFound = new NotFound<ProductId>(ProductId.New());
        DeleteProductResult error = new Error("boom", "BOOM");
        DeleteProductResult notAuthorized = new NotAuthorized(["not an administrator"]);

        Assert.True(DeleteProductResult.ShouldCommit(success));
        Assert.False(DeleteProductResult.ShouldCommit(notFound));
        Assert.False(DeleteProductResult.ShouldCommit(error));
        Assert.False(DeleteProductResult.ShouldCommit(notAuthorized));
    }

    /// <summary>Verifies <see cref="DeleteProductResult.FromNotAuthorized"/> builds a union instance carrying the given <see cref="NotAuthorized"/>.</summary>
    [Fact]
    public void FromNotAuthorized_carries_the_given_reasons()
    {
        var notAuthorized = new NotAuthorized(["not an administrator"]);

        DeleteProductResult result = DeleteProductResult.FromNotAuthorized(notAuthorized);

        var reasons = Assert
            .IsType<NotAuthorized>(((System.Runtime.CompilerServices.IUnion)result).Value)
            .Reasons;
        Assert.Equal(["not an administrator"], reasons);
    }
}
