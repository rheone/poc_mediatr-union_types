// TODO: excluded from CSharpier via .csharpierignore (union declarations crash CSharpier 1.3.0's
// parser). To reverse: remove this file's entry from .csharpierignore, run
// `dotnet csharpier check .`, and delete this comment if it passes.
using System.Runtime.CompilerServices;
using MediatrUnionPoc.Application.Common.Abstractions;
using MediatrUnionPoc.Application.Common.Results;

namespace MediatrUnionPoc.Application.Tests.Unions;

/// <summary>
/// No handler in this POC currently produces <see cref="Failure"/> — see its remarks for why it
/// exists anyway. <see cref="NotAuthorized"/> does have a real producer now
/// (<c>DeleteProductResult</c>, via <c>AuthorizationBehavior</c>), but this scratch union still
/// exercises both together: proof they behave correctly (pattern matching, implicit conversion,
/// transaction classification) on a union that has nothing to do with either's actual producer,
/// rather than only ever being observed through <c>DeleteProductResult</c> specifically.
/// </summary>
public union AdminActionResult(Success, Failure, NotAuthorized) : ITransactionOutcome<AdminActionResult>
{
    /// <summary>
    /// Classifies which of <see cref="AdminActionResult"/>'s cases should commit a transaction
    /// versus roll it back: only <see cref="Success"/> commits.
    /// </summary>
    /// <param name="response">The union instance to classify.</param>
    /// <returns><see langword="true"/> if the transaction should commit; otherwise, <see langword="false"/>.</returns>
    public static bool ShouldCommit(AdminActionResult response) => response switch
    {
        Success => true,
        Failure => false,
        NotAuthorized => false,
    };
}

/// <summary>
/// Exercises <see cref="Failure"/> and <see cref="NotAuthorized"/> via the scratch
/// <see cref="AdminActionResult"/> union, independent of either case type's real producer (or, for
/// <see cref="Failure"/>, lack of one), so their conversion, pattern-matching, and
/// transaction-classification behavior is proven generically.
/// </summary>
public class FailureAndNotAuthorizedCaseTypeTests
{
    /// <summary>
    /// Verifies <see cref="Failure"/> implicitly converts into the union and unwraps via pattern
    /// matching exactly like any other declared case type.
    /// </summary>
    [Fact]
    public void Failure_converts_implicitly_into_the_union_and_unwraps()
    {
        // Arrange
        var expectedReasons = new[] { "insufficient stock", "product discontinued" };

        // Act
        AdminActionResult result = new Failure(expectedReasons);

        // Assert
        var reasons = Assert.IsType<Failure>(((IUnion)result).Value).Reasons;
        Assert.Equal(expectedReasons, reasons);
    }

    /// <summary>
    /// Verifies <see cref="NotAuthorized"/> implicitly converts into the union and unwraps via
    /// pattern matching exactly like any other declared case type.
    /// </summary>
    [Fact]
    public void NotAuthorized_converts_implicitly_into_the_union_and_unwraps()
    {
        // Arrange
        var expectedReasons = new[] { "missing admin role" };

        // Act
        AdminActionResult result = new NotAuthorized(expectedReasons);

        // Assert
        var reasons = Assert.IsType<NotAuthorized>(((IUnion)result).Value).Reasons;
        Assert.Equal(expectedReasons, reasons);
    }

    /// <summary>Rows: a <see cref="Failure"/> and a <see cref="NotAuthorized"/> — different case types, both rollback.</summary>
    public static TheoryData<AdminActionResult> ShouldCommit_rollback_Test_Data =>
        [
            new AdminActionResult(new Failure(["business rule violated"])),
            new AdminActionResult(new NotAuthorized(["missing admin role"])),
        ];

    /// <summary>
    /// Verifies both <see cref="Failure"/> and <see cref="NotAuthorized"/> classify as rollback
    /// via <see cref="AdminActionResult.ShouldCommit"/>, even though they carry different reasons.
    /// </summary>
    /// <param name="response">The rollback-case instance under test.</param>
    [Theory]
    [MemberData(nameof(ShouldCommit_rollback_Test_Data))]
    public void ShouldCommit_Failure_and_NotAuthorized_classify_as_rollback(
        AdminActionResult response
    )
    {
        // Arrange (response supplied by ShouldCommit_rollback_Test_Data)

        // Act
        var shouldCommit = AdminActionResult.ShouldCommit(response);

        // Assert
        Assert.False(shouldCommit);
    }

    /// <summary>
    /// Verifies <see cref="Success"/> still classifies as commit once <see cref="Failure"/> and
    /// <see cref="NotAuthorized"/> are added to the same union's case set.
    /// </summary>
    [Fact]
    public void ShouldCommit_Success_classifies_as_commit_alongside_the_other_cases()
    {
        // Arrange
        AdminActionResult response = new Success();

        // Act
        var shouldCommit = AdminActionResult.ShouldCommit(response);

        // Assert
        Assert.True(shouldCommit);
    }
}
