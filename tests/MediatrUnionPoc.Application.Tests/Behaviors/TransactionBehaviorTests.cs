// TODO: excluded from CSharpier via .csharpierignore (union declarations crash CSharpier 1.3.0's
// parser). To reverse: remove this file's entry from .csharpierignore, run
// `dotnet csharpier check .`, and delete this comment if it passes.
using System.Security.Claims;
using MediatrUnionPoc.Application.Common.Abstractions;
using MediatrUnionPoc.Application.Common.Behaviors;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Features.Products.Delete;
using MediatrUnionPoc.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace MediatrUnionPoc.Application.Tests.Behaviors;

/// <summary>
/// A case type with no relationship whatsoever to <see cref="Error"/>/<see cref="Failure"/>/
/// <see cref="NotAuthorized"/>/<see cref="ValidationErrors"/>/<see cref="NotFound{TId}"/> — standing in
/// for "a dev's own arbitrary record, never seen by TransactionBehavior's author." If commit/rollback
/// were ever decided by inspecting known case types, this one would be silently misclassified.
/// </summary>
public sealed record SomeDevsOwnCaseType;

/// <summary>
/// A minimal command whose union declares <see cref="SomeDevsOwnCaseType"/> as a case meaning
/// "don't commit" — a meaning private to this union, not a property of the case type itself.
/// </summary>
public sealed record ArbitraryCommand : ITransactionalCommand<ArbitraryOutcome>;

/// <summary>
/// The response union for <see cref="ArbitraryCommand"/>, deciding commit/rollback via
/// <see cref="ShouldCommit(ArbitraryOutcome)"/> rather than by any inherent meaning of its case types.
/// </summary>
public union ArbitraryOutcome(Success, SomeDevsOwnCaseType) : ITransactionOutcome<ArbitraryOutcome>
{
    /// <summary>Maps <see cref="Success"/> to commit and <see cref="SomeDevsOwnCaseType"/> to rollback.</summary>
    /// <param name="response">The union instance returned by the handler.</param>
    /// <returns><see langword="true"/> when <paramref name="response"/> is <see cref="Success"/>; otherwise <see langword="false"/>.</returns>
    public static bool ShouldCommit(ArbitraryOutcome response) => response switch
    {
        Success => true,
        SomeDevsOwnCaseType => false,
    };
}

/// <summary>Verifies commit/rollback is decided purely by which union case the handler returned — no exceptions involved for the expected error cases.</summary>
public class TransactionBehaviorTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly TransactionBehavior<DeleteProductCommand, DeleteProductResult> _sut;

    /// <summary>Wires up <see cref="_sut"/> against the substituted <see cref="_unitOfWork"/>.</summary>
    public TransactionBehaviorTests()
    {
        _sut = new TransactionBehavior<DeleteProductCommand, DeleteProductResult>(
            _unitOfWork,
            NullLogger<TransactionBehavior<DeleteProductCommand, DeleteProductResult>>.Instance);
    }

    /// <summary>Verifies <see cref="IUnitOfWork.CommitAsync(CancellationToken)"/> is called, and rollback is not, when the handler returns a success case.</summary>
    /// <returns>The asynchronous test operation.</returns>
    [Fact]
    public async Task Commits_when_the_handler_returns_a_success_case()
    {
        DeleteProductResult response = new Success();

        await _sut.Handle(new DeleteProductCommand(Guid.NewGuid(), new ClaimsPrincipal()), _ => Task.FromResult(response), CancellationToken.None);

        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().RollbackAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies rollback is called, and commit is not, when the handler returns any error case from <see cref="ErrorCases"/>.</summary>
    /// <param name="response">The error-case response the handler returns.</param>
    /// <returns>The asynchronous test operation.</returns>
    [Theory]
    [MemberData(nameof(ErrorCases))]
    public async Task Rolls_back_when_the_handler_returns_an_error_case(DeleteProductResult response)
    {
        await _sut.Handle(new DeleteProductCommand(Guid.NewGuid(), new ClaimsPrincipal()), _ => Task.FromResult(response), CancellationToken.None);

        await _unitOfWork.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>The error cases <see cref="Rolls_back_when_the_handler_returns_an_error_case(DeleteProductResult)"/> is theorized over.</summary>
    /// <returns>A <see cref="TheoryData{T}"/> of <see cref="DeleteProductResult"/> error cases.</returns>
    public static TheoryData<DeleteProductResult> ErrorCases() =>
        [
            new DeleteProductResult(new NotFound<ProductId>(ProductId.New())),
            new DeleteProductResult(new Error("boom", "BOOM")),
        ];

    /// <summary>Verifies rollback is decided by asking <see cref="ArbitraryOutcome.ShouldCommit(ArbitraryOutcome)"/>, not by recognizing <see cref="SomeDevsOwnCaseType"/> as a known case type.</summary>
    /// <returns>The asynchronous test operation.</returns>
    [Fact]
    public async Task Rolls_back_for_an_arbitrary_case_type_the_behavior_has_never_seen_before()
    {
        var sut = new TransactionBehavior<ArbitraryCommand, ArbitraryOutcome>(
            _unitOfWork,
            NullLogger<TransactionBehavior<ArbitraryCommand, ArbitraryOutcome>>.Instance);

        // SomeDevsOwnCaseType means "don't commit" per ArbitraryOutcome.ShouldCommit — a meaning
        // TransactionBehavior cannot know by inspecting the type, only by asking the union.
        ArbitraryOutcome response = new SomeDevsOwnCaseType();

        await sut.Handle(new ArbitraryCommand(), _ => Task.FromResult(response), CancellationToken.None);

        await _unitOfWork.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies rollback is called and the exception propagates when the handler throws instead of returning a union case.</summary>
    /// <returns>The asynchronous test operation.</returns>
    [Fact]
    public async Task Rolls_back_and_rethrows_when_the_handler_throws()
    {
        static Task<DeleteProductResult> ThrowingAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("infra failure");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.Handle(new DeleteProductCommand(Guid.NewGuid(), new ClaimsPrincipal()), ThrowingAsync, CancellationToken.None));

        await _unitOfWork.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}
