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
    // SWEEP-AMBIGUITY: FromNotAuthorized(notAuthorized) has no ArgumentNullException guard (a null argument is
    // silently wrapped into the union) / a null NotAuthorized should throw ArgumentNullException naming
    // "notAuthorized", but no such test is written because production does not do that.
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
}
