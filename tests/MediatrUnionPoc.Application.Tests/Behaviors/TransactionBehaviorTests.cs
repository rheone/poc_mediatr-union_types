// TODO: excluded from CSharpier via .csharpierignore (union declarations crash CSharpier 1.3.0's
// parser). To reverse: remove this file's entry from .csharpierignore, run
// `dotnet csharpier check .`, and delete this comment if it passes.
using MediatrUnionPoc.Application.Common.Abstractions;
using MediatrUnionPoc.Application.Common.Behaviors;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Features.Products.Delete;
using MediatrUnionPoc.Application.Tests.TestData;
using MediatrUnionPoc.Domain;
using Microsoft.Extensions.Logging;
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
public union ArbitraryOutcome(Success, SomeDevsOwnCaseType)
    : ITransactionOutcome<ArbitraryOutcome>,
        ICommitFailable<ArbitraryOutcome>
{
    /// <summary>Reports every commit failure as <see cref="SomeDevsOwnCaseType"/> — this union has no finer classification.</summary>
    /// <param name="failure">How the commit failed.</param>
    /// <returns>A <see cref="SomeDevsOwnCaseType"/>.</returns>
    public static ArbitraryOutcome FromCommitFailure(CommitFailure failure) => failure switch
    {
        ConcurrencyConflict => new SomeDevsOwnCaseType(),
        UniqueViolation => new SomeDevsOwnCaseType(),
    };

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
    private const string ErrorMessage = "boom";
    private const string ErrorCode = "BOOM";
    private const string InfrastructureFailureMessage = "infra failure";

    private static readonly Guid ProductGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly IUnitOfWork _unitOfWork = UnitOfWorkMother.Committing();
    private readonly TransactionBehavior<DeleteProductCommand, DeleteProductResult> _sut;

    /// <summary>Wires up <see cref="_sut"/> against the substituted <see cref="_unitOfWork"/>.</summary>
    public TransactionBehaviorTests()
    {
        _sut = new TransactionBehavior<DeleteProductCommand, DeleteProductResult>(
            _unitOfWork,
            NullLogger<TransactionBehavior<DeleteProductCommand, DeleteProductResult>>.Instance);
    }

    /// <summary>The error cases <see cref="Handle_ErrorCase_RollsBackAndDoesNotCommit_Test(DeleteProductResult)"/> is theorized over.</summary>
    public static TheoryData<DeleteProductResult> Handle_ErrorCase_RollsBackAndDoesNotCommit_Test_Data =>
        new(
            Row("NotFound", new DeleteProductResult(new NotFound<ProductId>(ProductId.From(ProductGuid)))),
            Row("Error", new DeleteProductResult(new Error(ErrorMessage, ErrorCode)))
        );

    // Union values have no distinguishing ToString, so each row names its case explicitly to keep
    // display names unique.
    private static TheoryDataRow<DeleteProductResult> Row(string caseName, DeleteProductResult response) =>
        new(response)
        {
            TestDisplayName = $"{nameof(Handle_ErrorCase_RollsBackAndDoesNotCommit_Test)}(case: {caseName})",
        };

    /// <summary>Verifies <see cref="IUnitOfWork.CommitAsync(CancellationToken)"/> is called, and rollback is not, when the handler returns a success case.</summary>
    /// <returns>The asynchronous test operation.</returns>
    [Fact]
    public async Task Handle_SuccessCase_CommitsAndDoesNotRollBack_Test()
    {
        // Arrange
        DeleteProductResult response = new Success();

        // Act
        await _sut.Handle(DeleteCommand(), _ => Task.FromResult(response), CancellationToken.None);

        // Assert
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().RollbackAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies rollback is called, and commit is not, when the handler returns any error case from <see cref="Handle_ErrorCase_RollsBackAndDoesNotCommit_Test_Data"/>.</summary>
    /// <param name="response">The error-case response the handler returns.</param>
    /// <returns>The asynchronous test operation.</returns>
    [Theory]
    [MemberData(nameof(Handle_ErrorCase_RollsBackAndDoesNotCommit_Test_Data))]
    public async Task Handle_ErrorCase_RollsBackAndDoesNotCommit_Test(DeleteProductResult response)
    {
        // Arrange
        var command = DeleteCommand();

        // Act
        await _sut.Handle(command, _ => Task.FromResult(response), CancellationToken.None);

        // Assert
        await _unitOfWork.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies rollback is decided by asking <see cref="ArbitraryOutcome.ShouldCommit(ArbitraryOutcome)"/>, not by recognizing <see cref="SomeDevsOwnCaseType"/> as a known case type.</summary>
    /// <returns>The asynchronous test operation.</returns>
    [Fact]
    public async Task Handle_ArbitraryCaseType_RollsBack_Test()
    {
        // Arrange
        var sut = new TransactionBehavior<ArbitraryCommand, ArbitraryOutcome>(
            _unitOfWork,
            NullLogger<TransactionBehavior<ArbitraryCommand, ArbitraryOutcome>>.Instance);

        // SomeDevsOwnCaseType means "don't commit" per ArbitraryOutcome.ShouldCommit — a meaning
        // TransactionBehavior cannot know by inspecting the type, only by asking the union.
        ArbitraryOutcome response = new SomeDevsOwnCaseType();

        // Act
        await sut.Handle(new ArbitraryCommand(), _ => Task.FromResult(response), CancellationToken.None);

        // Assert
        await _unitOfWork.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies rollback is called and the exception propagates when the handler throws instead of returning a union case.</summary>
    /// <returns>The asynchronous test operation.</returns>
    [Fact]
    public async Task Handle_HandlerThrows_RollsBackAndRethrows_Test()
    {
        // Arrange
        const string message = InfrastructureFailureMessage;
        static Task<DeleteProductResult> ThrowingAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(message);

        // Act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.Handle(DeleteCommand(), ThrowingAsync, CancellationToken.None));

        // Assert
        Assert.Equal(message, ex.Message);
        await _unitOfWork.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies the transaction is begun before the handler's response is classified, so commit never precedes begin.</summary>
    /// <returns>The asynchronous test operation.</returns>
    // Auto Generated, verify expected behavior:
    [Fact]
    public async Task Handle_SuccessCase_BeginsTransactionBeforeCommit_Test()
    {
        // Arrange
        DeleteProductResult response = new Success();

        // Act
        await _sut.Handle(DeleteCommand(), _ => Task.FromResult(response), CancellationToken.None);

        // Assert
        Received.InOrder(() =>
        {
            _ = _unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>());
            _ = _unitOfWork.CommitAsync(Arg.Any<CancellationToken>());
        });
    }

    /// <summary>Verifies the caller's cancellation token is passed to begin, <c>next</c>, and commit rather than replaced.</summary>
    /// <returns>The asynchronous test operation.</returns>
    // Auto Generated, verify expected behavior:
    [Fact]
    public async Task Handle_SuccessCase_ForwardsCancellationToken_Test()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        CancellationToken? tokenSeenByNext = null;

        // Act
        await _sut.Handle(
            DeleteCommand(),
            token =>
            {
                tokenSeenByNext = token;
                return Task.FromResult<DeleteProductResult>(new Success());
            },
            cts.Token);

        // Assert
        Assert.Equal(cts.Token, tokenSeenByNext);
        await _unitOfWork.Received(1).BeginTransactionAsync(cts.Token);
        await _unitOfWork.Received(1).CommitAsync(cts.Token);
    }

    private static DeleteProductCommand DeleteCommand() =>
        new(ProductGuid, PrincipalMother.Anonymous());

    /// <summary>Verifies the constructor rejects a null unit of work instead of failing on first use.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void Ctor_NullUnitOfWork_ThrowsArgumentNullException_Test()
    {
        // Arrange
        var logger = NullLogger<TransactionBehavior<DeleteProductCommand, DeleteProductResult>>.Instance;

        // Act
        var ex = Assert.Throws<ArgumentNullException>(() => new TransactionBehavior<DeleteProductCommand, DeleteProductResult>(null!, logger));

        // Assert
        Assert.Equal("unitOfWork", ex.ParamName);
    }

    /// <summary>Verifies the constructor rejects a null logger instead of failing on first use.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void Ctor_NullLogger_ThrowsArgumentNullException_Test()
    {
        // Arrange
        var unitOfWork = _unitOfWork;

        // Act
        var ex = Assert.Throws<ArgumentNullException>(() => new TransactionBehavior<DeleteProductCommand, DeleteProductResult>(unitOfWork, null!));

        // Assert
        Assert.Equal("logger", ex.ParamName);
    }

    /// <summary>Verifies a null request is rejected with <see cref="ArgumentNullException"/> before any transaction is begun.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    // Auto Generated, verify expected behavior:
    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException_Test()
    {
        // Arrange
        DeleteProductResult response = new Success();

        // Act
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => _sut.Handle(null!, _ => Task.FromResult(response), CancellationToken.None));

        // Assert
        Assert.Equal("request", ex.ParamName);
        await _unitOfWork.DidNotReceive().BeginTransactionAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().RollbackAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies a null <c>next</c> delegate is rejected with <see cref="ArgumentNullException"/> before any transaction is begun, rather than surfacing later as a <see cref="NullReferenceException"/> that rolls one back.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    // Auto Generated, verify expected behavior:
    [Fact]
    public async Task Handle_NullNext_ThrowsArgumentNullException_Test()
    {
        // Arrange
        var command = DeleteCommand();

        // Act
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => _sut.Handle(command, null!, CancellationToken.None));

        // Assert
        Assert.Equal("next", ex.ParamName);
        await _unitOfWork.DidNotReceive().BeginTransactionAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().RollbackAsync(Arg.Any<CancellationToken>());
    }
}

/// <summary>
/// Verifies what <see cref="TransactionBehavior{TRequest,TResponse}"/> logs: an Information line
/// naming the request and case when the union says to roll back, an Error line carrying the
/// original exception when the handler throws, and nothing on a commit.
/// </summary>
public class TransactionBehaviorLoggingTests
{
    private const string ErrorMessage = "boom";
    private const string ErrorCode = "BOOM";
    private const string InfrastructureFailureMessage = "infra failure";
    private const string RollbackTemplate = "{RequestName} produced {ResultCase}; rolling back transaction";
    private const string ThrewTemplate = "{RequestName} threw; rolling back transaction";
    private const string RequestNameKey = "RequestName";
    private const string ResultCaseKey = "ResultCase";

    private static readonly Guid ProductGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly IUnitOfWork _unitOfWork = UnitOfWorkMother.Committing();
    private readonly CapturingLogger<TransactionBehavior<DeleteProductCommand, DeleteProductResult>> _logger = new();
    private readonly TransactionBehavior<DeleteProductCommand, DeleteProductResult> _sut;

    /// <summary>Wires up <see cref="_sut"/> against a substituted unit of work and a capturing logger.</summary>
    public TransactionBehaviorLoggingTests() =>
        _sut = new TransactionBehavior<DeleteProductCommand, DeleteProductResult>(_unitOfWork, _logger);

    /// <summary>The rollback-producing cases <see cref="Handle_RollbackCase_LogsInformationWithRequestNameAndCase_Test(DeleteProductResult, string)"/> is theorized over, with the case name each should log.</summary>
    public static TheoryData<DeleteProductResult, string> Handle_RollbackCase_LogsInformationWithRequestNameAndCase_Test_Data =>
        new()
        {
            { new DeleteProductResult(new NotFound<ProductId>(ProductId.From(ProductGuid))), typeof(NotFound<ProductId>).Name },
            { new DeleteProductResult(new Error(ErrorMessage, ErrorCode)), nameof(Error) },
        };

    /// <summary>Verifies a rollback decision logs one Information line naming the request type and the returned case.</summary>
    /// <param name="response">The rollback-producing response the handler returns.</param>
    /// <param name="expectedCase">The case name expected in the log.</param>
    /// <returns>The asynchronous test operation.</returns>
    // Auto Generated, verify expected behavior:
    [Theory]
    [MemberData(nameof(Handle_RollbackCase_LogsInformationWithRequestNameAndCase_Test_Data))]
    public async Task Handle_RollbackCase_LogsInformationWithRequestNameAndCase_Test(DeleteProductResult response, string expectedCase)
    {
        // Arrange
        var command = DeleteCommand();

        // Act
        await _sut.Handle(command, _ => Task.FromResult(response), TestContext.Current.CancellationToken);

        // Assert
        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal(RollbackTemplate, entry.Template);
        Assert.Equal($"{nameof(DeleteProductCommand)} produced {expectedCase}; rolling back transaction", entry.Message);
        Assert.Equal(nameof(DeleteProductCommand), entry.Properties[RequestNameKey]);
        Assert.Equal(expectedCase, entry.Properties[ResultCaseKey]);
        Assert.Null(entry.Exception);
    }

    /// <summary>Verifies a commit writes nothing to the log.</summary>
    /// <returns>The asynchronous test operation.</returns>
    // Auto Generated, verify expected behavior:
    [Fact]
    public async Task Handle_SuccessCase_LogsNothing_Test()
    {
        // Arrange
        DeleteProductResult response = new Success();

        // Act
        await _sut.Handle(DeleteCommand(), _ => Task.FromResult(response), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(_logger.Entries);
    }

    /// <summary>Verifies a throwing handler logs one Error line carrying the original exception instance and the request name, with no result case.</summary>
    /// <returns>The asynchronous test operation.</returns>
    // Auto Generated, verify expected behavior:
    [Fact]
    public async Task Handle_HandlerThrows_LogsErrorWithExceptionAndRequestName_Test()
    {
        // Arrange
        var thrown = new InvalidOperationException(InfrastructureFailureMessage);

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.Handle(DeleteCommand(), _ => throw thrown, TestContext.Current.CancellationToken));

        // Assert
        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal(ThrewTemplate, entry.Template);
        Assert.Equal($"{nameof(DeleteProductCommand)} threw; rolling back transaction", entry.Message);
        Assert.Same(thrown, entry.Exception);
        Assert.Equal(nameof(DeleteProductCommand), entry.Properties[RequestNameKey]);
        Assert.False(entry.Properties.ContainsKey(ResultCaseKey));
    }

    /// <summary>Verifies a cancellation the caller asked for (a hung-up client, a request timeout) is rolled back and rethrown but logged at Information rather than Error, and that the rollback is not handed the already-cancelled token.</summary>
    /// <returns>The asynchronous test operation.</returns>
    [Fact]
    public async Task Handle_CancelledWhileRunning_RollsBackWithoutErrorAndWithAnUncancelledToken_Test()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var token = cts.Token;

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _sut.Handle(DeleteCommand(), _ => throw new OperationCanceledException(token), token));

        // Assert
        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal($"{nameof(DeleteProductCommand)} was cancelled; rolling back transaction", entry.Message);
        await _unitOfWork.Received(1).RollbackAsync(CancellationToken.None);
    }

    private static DeleteProductCommand DeleteCommand() =>
        new(ProductGuid, PrincipalMother.Anonymous());
}
