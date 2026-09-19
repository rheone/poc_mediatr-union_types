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
    private const string InsufficientStock = "insufficient stock";
    private const string ProductDiscontinued = "product discontinued";
    private const string MissingAdminRole = "missing admin role";
    private const string BusinessRuleViolated = "business rule violated";

    /// <summary>
    /// Verifies <see cref="Failure"/> implicitly converts into the union and unwraps via pattern
    /// matching exactly like any other declared case type.
    /// </summary>
    [Fact]
    public void ImplicitConversion_Failure_UnwrapsViaValueWithSameReasons_Test()
    {
        // Arrange
        var expectedReasons = new[] { InsufficientStock, ProductDiscontinued };

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
    public void ImplicitConversion_NotAuthorized_UnwrapsViaValueWithSameReasons_Test()
    {
        // Arrange
        var expectedReasons = new[] { MissingAdminRole };

        // Act
        AdminActionResult result = new NotAuthorized(expectedReasons);

        // Assert
        var reasons = Assert.IsType<NotAuthorized>(((IUnion)result).Value).Reasons;
        Assert.Equal(expectedReasons, reasons);
    }

    /// <summary>Rows: a <see cref="Failure"/> and a <see cref="NotAuthorized"/> — different case types, both rollback.</summary>
    public static TheoryData<AdminActionResult> ShouldCommit_FailureOrNotAuthorized_ReturnsFalse_Test_Data =>
        new(
            Row("Failure", new AdminActionResult(new Failure([BusinessRuleViolated]))),
            Row("NotAuthorized", new AdminActionResult(new NotAuthorized([MissingAdminRole])))
        );

    // Union values have no distinguishing ToString, so each row names its case explicitly to keep
    // display names unique.
    private static TheoryDataRow<AdminActionResult> Row(string caseName, AdminActionResult response) =>
        new(response)
        {
            TestDisplayName = $"{nameof(ShouldCommit_FailureOrNotAuthorized_ReturnsFalse_Test)}(case: {caseName})",
        };

    /// <summary>
    /// Verifies both <see cref="Failure"/> and <see cref="NotAuthorized"/> classify as rollback
    /// via <see cref="AdminActionResult.ShouldCommit"/>, even though they carry different reasons.
    /// </summary>
    /// <param name="response">The rollback-case instance under test.</param>
    [Theory]
    [MemberData(nameof(ShouldCommit_FailureOrNotAuthorized_ReturnsFalse_Test_Data))]
    public void ShouldCommit_FailureOrNotAuthorized_ReturnsFalse_Test(
        AdminActionResult response
    )
    {
        // Arrange (response supplied by ShouldCommit_FailureOrNotAuthorized_ReturnsFalse_Test_Data)

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
    public void ShouldCommit_Success_ReturnsTrue_Test()
    {
        // Arrange
        AdminActionResult response = new Success();

        // Act
        var shouldCommit = AdminActionResult.ShouldCommit(response);

        // Assert
        Assert.True(shouldCommit);
    }
}
