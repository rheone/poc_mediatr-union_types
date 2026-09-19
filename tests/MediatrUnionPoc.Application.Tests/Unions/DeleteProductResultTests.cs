using System.Runtime.CompilerServices;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Features.Products.Delete;
using MediatrUnionPoc.Application.Features.Products.Update;
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
    private const string ErrorMessage = "boom";
    private const string ErrorCode = "BOOM";
    private const string NotAnAdministrator = "not an administrator";

    private static readonly ProductId SomeProductId = ProductId.From(
        Guid.Parse("66666666-6666-6666-6666-666666666666")
    );

    /// <summary>Rows: one per declared case, with whether that case should commit — only <see cref="Success"/> does.</summary>
    public static TheoryData<
        DeleteProductResult,
        bool
    > ShouldCommit_EachDeclaredCase_CommitsOnlyForSuccess_Test_Data =>
        new()
        {
            { new Success(), true },
            { new NotFound<ProductId>(SomeProductId), false },
            { new Error(ErrorMessage, ErrorCode), false },
            { new NotAuthorized([NotAnAdministrator]), false },
        };

    /// <summary>Verifies <see cref="DeleteProductResult.ShouldCommit"/> commits only for its <see cref="Success"/> case.</summary>
    /// <param name="response">The union instance to classify.</param>
    /// <param name="expected">Whether the case is expected to commit.</param>
    [Theory]
    [MemberData(nameof(ShouldCommit_EachDeclaredCase_CommitsOnlyForSuccess_Test_Data))]
    public void ShouldCommit_EachDeclaredCase_CommitsOnlyForSuccess_Test(
        DeleteProductResult response,
        bool expected
    )
    {
        // Arrange (response supplied by ShouldCommit_EachDeclaredCase_CommitsOnlyForSuccess_Test_Data)

        // Act
        var shouldCommit = DeleteProductResult.ShouldCommit(response);

        // Assert
        Assert.Equal(expected, shouldCommit);
    }

    /// <summary>Verifies <see cref="DeleteProductResult.FromNotAuthorized"/> builds a union instance carrying the given <see cref="NotAuthorized"/>.</summary>
    [Fact]
    public void FromNotAuthorized_GivenNotAuthorized_CarriesReasons_Test()
    {
        // Arrange
        var notAuthorized = new NotAuthorized([NotAnAdministrator]);

        // Act
        DeleteProductResult result = DeleteProductResult.FromNotAuthorized(notAuthorized);

        // Assert
        var reasons = Assert.IsType<NotAuthorized>(((IUnion)result).Value).Reasons;
        Assert.Equal([NotAnAdministrator], reasons);
    }

    /// <summary>Verifies <see cref="DeleteProductResult.FromNotAuthorized"/> rejects a null argument instead of silently wrapping it into the union.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void FromNotAuthorized_DeleteProductResultNullNotAuthorized_ThrowsArgumentNullException_Test()
    {
        // Act
        var ex = Assert.Throws<ArgumentNullException>(() =>
            DeleteProductResult.FromNotAuthorized(null!)
        );

        // Assert
        Assert.Equal("notAuthorized", ex.ParamName);
    }

    /// <summary>Verifies <see cref="UpdateProductResult.FromNotAuthorized"/> rejects a null argument instead of silently wrapping it into the union.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void FromNotAuthorized_UpdateProductResultNullNotAuthorized_ThrowsArgumentNullException_Test()
    {
        // Act
        var ex = Assert.Throws<ArgumentNullException>(() =>
            UpdateProductResult.FromNotAuthorized(null!)
        );

        // Assert
        Assert.Equal("notAuthorized", ex.ParamName);
    }
}
