using System.Runtime.CompilerServices;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Features.Products.Common;
using MediatrUnionPoc.Application.Features.Products.Patch;
using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Application.Tests.Unions;

/// <summary>Verifies <see cref="PatchProductResult"/> classifies each of its cases for the pipeline behaviors that consult it, exactly as <c>UpdateProductResult</c> does.</summary>
public class PatchProductResultTests
{
    private static readonly ProductId SomeProductId = ProductId.From(
        Guid.Parse("88888888-8888-8888-8888-888888888888")
    );

    /// <summary>Verifies only the product case commits; every failure or refusal rolls back.</summary>
    [Fact]
    public void ShouldCommit_EachCase_CommitsOnlyForProductDto_Test()
    {
        // Arrange
        (string CaseName, PatchProductResult Response, bool Expected)[] cases =
        [
            (
                "ProductDto",
                new ProductDto(
                    SomeProductId,
                    "Widget",
                    9.99m,
                    ProductVersion.Initial,
                    DateTimeOffset.UnixEpoch
                ),
                true
            ),
            ("NotFound", new NotFound<ProductId>(SomeProductId), false),
            ("ValidationErrors", new ValidationErrors([new ValidationError("Name", "bad")]), false),
            ("Error", new Error("boom", "BOOM"), false),
            ("NotAuthorized", new NotAuthorized(["not the owner"]), false),
            ("PreconditionFailed", new PreconditionFailed("stale"), false),
            ("Conflict", new Conflict("name taken"), false),
        ];

        // Act
        var actual = cases.Select(c => (c.CaseName, PatchProductResult.ShouldCommit(c.Response)));

        // Assert
        Assert.Equal(cases.Select(c => (c.CaseName, c.Expected)), actual);
    }

    /// <summary>Verifies validation errors are carried through unchanged so <c>ValidationBehavior</c> can short-circuit generically.</summary>
    [Fact]
    public void FromValidationErrors_Errors_WrapsThemUnchanged_Test()
    {
        // Arrange
        var errors = new ValidationErrors([new ValidationError("Name", "bad")]);

        // Act
        var result = PatchProductResult.FromValidationErrors(errors);

        // Assert
        Assert.Same(errors, ((IUnion)result).Value);
    }

    /// <summary>Verifies a null argument is rejected rather than wrapped.</summary>
    [Fact]
    public void FromValidationErrors_Null_ThrowsArgumentNullException_Test()
    {
        // Act
        var ex = Assert.Throws<ArgumentNullException>(() =>
            PatchProductResult.FromValidationErrors(null!)
        );

        // Assert
        Assert.Equal("errors", ex.ParamName);
    }

    /// <summary>Verifies a denial is carried through unchanged so <c>AuthorizationBehavior</c> can short-circuit generically.</summary>
    [Fact]
    public void FromNotAuthorized_Denial_WrapsItUnchanged_Test()
    {
        // Arrange
        var denial = new NotAuthorized(["not the owner"]);

        // Act
        var result = PatchProductResult.FromNotAuthorized(denial);

        // Assert
        Assert.Same(denial, ((IUnion)result).Value);
    }

    /// <summary>Verifies a null denial is rejected rather than wrapped.</summary>
    [Fact]
    public void FromNotAuthorized_Null_ThrowsArgumentNullException_Test()
    {
        // Act
        var ex = Assert.Throws<ArgumentNullException>(() =>
            PatchProductResult.FromNotAuthorized(null!)
        );

        // Assert
        Assert.Equal("notAuthorized", ex.ParamName);
    }

    /// <summary>Verifies a stale write detected at commit time is a precondition failure, the same outcome as a stale version caught up front.</summary>
    [Fact]
    public void FromCommitFailure_ConcurrencyConflict_IsPreconditionFailed_Test()
    {
        // Act
        var result = PatchProductResult.FromCommitFailure(new ConcurrencyConflict());

        // Assert
        Assert.IsType<PreconditionFailed>(((IUnion)result).Value);
    }

    /// <summary>Verifies a unique-index violation at commit time is a conflict: a concurrent request claimed the name first.</summary>
    [Fact]
    public void FromCommitFailure_UniqueViolation_IsConflict_Test()
    {
        // Act
        var result = PatchProductResult.FromCommitFailure(new UniqueViolation());

        // Assert
        Assert.IsType<Conflict>(((IUnion)result).Value);
    }
}
