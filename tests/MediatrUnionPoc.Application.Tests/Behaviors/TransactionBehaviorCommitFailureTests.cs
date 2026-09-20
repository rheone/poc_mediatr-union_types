using System.Runtime.CompilerServices;
using MediatrUnionPoc.Application.Common.Abstractions;
using MediatrUnionPoc.Application.Common.Behaviors;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace MediatrUnionPoc.Application.Tests.Behaviors;

/// <summary>A command whose union classifies every commit failure itself.</summary>
public sealed record CommitAwareCommand : ITransactionalCommand<CommitAwareOutcome>;

/// <summary>A case type carrying which commit failure the union decided it was.</summary>
/// <param name="Reason">The union's own label for the failure.</param>
public sealed record CommitRejected(string Reason);

/// <summary>A union deciding for itself what each commit failure means.</summary>
public union CommitAwareOutcome(Success, CommitRejected)
    : ITransactionOutcome<CommitAwareOutcome>,
        ICommitFailable<CommitAwareOutcome>
{
    /// <summary>Only <see cref="Success"/> commits.</summary>
    /// <param name="response">The union value.</param>
    /// <returns>Whether to commit.</returns>
    public static bool ShouldCommit(CommitAwareOutcome response) => response switch
    {
        Success => true,
        CommitRejected => false,
    };

    /// <summary>Labels each commit failure distinctly.</summary>
    /// <param name="failure">The failure.</param>
    /// <returns>A <see cref="CommitRejected"/> naming the failure.</returns>
    public static CommitAwareOutcome FromCommitFailure(CommitFailure failure) => failure switch
    {
        ConcurrencyConflict => new CommitRejected("concurrency"),
        UniqueViolation => new CommitRejected("unique"),
    };
}

/// <summary>Verifies a failed commit rolls back and is translated by the union, not thrown.</summary>
public class TransactionBehaviorCommitFailureTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly TransactionBehavior<CommitAwareCommand, CommitAwareOutcome> _sut;

    /// <summary>Wires up the behavior against a substituted unit of work.</summary>
    public TransactionBehaviorCommitFailureTests() =>
        _sut = new(
            _unitOfWork,
            NullLogger<TransactionBehavior<CommitAwareCommand, CommitAwareOutcome>>.Instance);

    /// <summary>Verifies a concurrency conflict at commit rolls back and yields the union's own mapping of it.</summary>
    /// <returns>The asynchronous test operation.</returns>
    [Fact]
    public async Task Handle_CommitReportsConcurrencyConflict_RollsBackAndReturnsUnionsMapping_Test()
    {
        // Arrange
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(new CommitResult(new ConcurrencyConflict()));
        CommitAwareOutcome handlerResponse = new Success();

        // Act
        var result = await _sut.Handle(new CommitAwareCommand(), _ => Task.FromResult(handlerResponse), CancellationToken.None);

        // Assert
        var rejected = Assert.IsType<CommitRejected>(((IUnion)result).Value);
        Assert.Equal("concurrency", rejected.Reason);
        await _unitOfWork.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
    }
}
