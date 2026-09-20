using System.Runtime.CompilerServices;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Features.Products.Create;
using MediatrUnionPoc.Application.Features.Products.Delete;
using MediatrUnionPoc.Application.Features.Products.Update;
using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Application.Tests.Unions;

/// <summary>Verifies each transactional union classifies every commit failure in its own terms.</summary>
public class FromCommitFailureTests
{
    /// <summary>Verifies a stale write at commit time is a precondition failure for an update — the same outcome as a stale version caught up front.</summary>
    [Fact]
    public void FromCommitFailure_UpdateConcurrencyConflict_IsPreconditionFailed_Test()
    {
        // Arrange
        CommitFailure failure = new ConcurrencyConflict();

        // Act
        var result = UpdateProductResult.FromCommitFailure(failure);

        // Assert
        Assert.IsType<PreconditionFailed>(((IUnion)result).Value);
    }

    /// <summary>Verifies a stale write at commit time is a precondition failure for a delete.</summary>
    [Fact]
    public void FromCommitFailure_DeleteConcurrencyConflict_IsPreconditionFailed_Test()
    {
        // Arrange
        CommitFailure failure = new ConcurrencyConflict();

        // Act
        var result = DeleteProductResult.FromCommitFailure(failure);

        // Assert
        Assert.IsType<PreconditionFailed>(((IUnion)result).Value);
    }

    /// <summary>Verifies a concurrency conflict on a brand-new row is an unexpected error for a create, since a new row cannot be stale.</summary>
    [Fact]
    public void FromCommitFailure_CreateConcurrencyConflict_IsError_Test()
    {
        // Arrange
        CommitFailure failure = new ConcurrencyConflict();

        // Act
        var result = CreateProductResult.FromCommitFailure(failure);

        // Assert
        Assert.IsType<Error>(((IUnion)result).Value);
    }

    /// <summary>Verifies a unique violation at commit time is a conflict for a create: a concurrent request claimed the name first.</summary>
    [Fact]
    public void FromCommitFailure_CreateUniqueViolation_IsConflict_Test()
    {
        // Arrange
        CommitFailure failure = new UniqueViolation();

        // Act
        var result = CreateProductResult.FromCommitFailure(failure);

        // Assert
        Assert.IsType<Conflict>(((IUnion)result).Value);
    }

    /// <summary>Verifies a unique violation at commit time is a conflict for an update.</summary>
    [Fact]
    public void FromCommitFailure_UpdateUniqueViolation_IsConflict_Test()
    {
        // Arrange
        CommitFailure failure = new UniqueViolation();

        // Act
        var result = UpdateProductResult.FromCommitFailure(failure);

        // Assert
        Assert.IsType<Conflict>(((IUnion)result).Value);
    }
}
