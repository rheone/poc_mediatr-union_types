// TODO: excluded from CSharpier via .csharpierignore (union declarations crash CSharpier 1.3.0's
// parser). To reverse: remove this file's entry from .csharpierignore, run
// `dotnet csharpier check .`, and delete this comment if it passes.
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
    public void Failure_implicitly_converts_and_pattern_matches_like_any_other_case()
    {
        AdminActionResult result = new Failure(["insufficient stock", "product discontinued"]);

        var reasons = Assert.IsType<Failure>(((System.Runtime.CompilerServices.IUnion)result).Value).Reasons;
        Assert.Equal(["insufficient stock", "product discontinued"], reasons);
    }

    /// <summary>
    /// Verifies <see cref="NotAuthorized"/> implicitly converts into the union and unwraps via
    /// pattern matching exactly like any other declared case type.
    /// </summary>
    [Fact]
    public void NotAuthorized_implicitly_converts_and_pattern_matches_like_any_other_case()
    {
        AdminActionResult result = new NotAuthorized(["missing admin role"]);

        var reasons = Assert.IsType<NotAuthorized>(((System.Runtime.CompilerServices.IUnion)result).Value).Reasons;
        Assert.Equal(["missing admin role"], reasons);
    }

    /// <summary>
    /// Verifies both <see cref="Failure"/> and <see cref="NotAuthorized"/> classify as rollback
    /// via <see cref="AdminActionResult.ShouldCommit"/>, even though they carry different reasons.
    /// </summary>
    /// <param name="response">The rollback-case instance under test, supplied by <see cref="RollbackCases"/>.</param>
    [Theory]
    [MemberData(nameof(RollbackCases))]
    public void Failure_and_NotAuthorized_both_classify_as_rollback(AdminActionResult response) => Assert.False(AdminActionResult.ShouldCommit(response));

    /// <summary>
    /// Supplies the <see cref="Failure"/> and <see cref="NotAuthorized"/> instances exercised by
    /// <see cref="Failure_and_NotAuthorized_both_classify_as_rollback"/>.
    /// </summary>
    /// <returns>The rollback-classifying <see cref="AdminActionResult"/> instances.</returns>
    public static TheoryData<AdminActionResult> RollbackCases() =>
        [
            new AdminActionResult(new Failure(["business rule violated"])),
            new AdminActionResult(new NotAuthorized(["missing admin role"])),
        ];

    /// <summary>
    /// Verifies <see cref="Success"/> still classifies as commit once <see cref="Failure"/> and
    /// <see cref="NotAuthorized"/> are added to the same union's case set.
    /// </summary>
    [Fact]
    public void Success_still_classifies_as_commit_alongside_the_new_cases()
    {
        AdminActionResult response = new Success();

        Assert.True(AdminActionResult.ShouldCommit(response));
    }
}
