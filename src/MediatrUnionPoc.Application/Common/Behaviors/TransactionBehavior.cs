using System.Runtime.CompilerServices;
using MediatR;
using MediatrUnionPoc.Application.Common.Abstractions;
using MediatrUnionPoc.Domain;
using Microsoft.Extensions.Logging;

namespace MediatrUnionPoc.Application.Common.Behaviors;

/// <summary>
/// Wraps <see cref="ITransactionalCommand{TResponse}"/> execution in a unit-of-work transaction,
/// committing or rolling back based on which case the returned union reports.
/// </summary>
/// <remarks>
/// <para>
/// A plain <see cref="ICommand{TResponse}"/> that never opts into
/// <see cref="ITransactionalCommand{TResponse}"/> (e.g. one that only publishes an event, with no
/// persistence to commit or roll back) simply doesn't match this behavior's generic constraints
/// and is skipped by the pipeline entirely — opting into a transaction is deliberate per command,
/// not automatic.
/// </para>
/// <para>
/// Commit-vs-rollback is decided by asking the union itself —
/// <see cref="ITransactionOutcome{TSelf}.ShouldCommit"/> — never by this behavior inspecting
/// which case type came back. Shared case types are meaning-free and reused across unions (an
/// <c>Error</c> in one union might mean something entirely different in another), so only the
/// union that declares a case type gets to say what that case means for its own transaction; this
/// behavior stays generic over every command without knowing any of their case types.
/// </para>
/// <para>
/// Because each union's <c>ShouldCommit</c> implementation is a <c>switch</c> over its own closed
/// set of case types, the compiler forces every case — including ones added after this behavior
/// was written — to be classified. There is no default branch here to silently commit (or roll
/// back) an unrecognized case, because this behavior never sees case types at all.
/// </para>
/// <para>
/// No exceptions are used for this branching: a domain-level failure (validation, not-found,
/// unauthorized, business-rule failure) is an ordinary, expected outcome, not an exceptional one.
/// A thrown exception still triggers a rollback, but is reserved for genuinely unexpected
/// infrastructure failures, and is rethrown unmodified after the rollback so the caller sees the
/// original exception.
/// </para>
/// </remarks>
/// <typeparam name="TRequest">The transactional command type.</typeparam>
/// <typeparam name="TResponse">The command's response union type.</typeparam>
/// <param name="unitOfWork">The unit of work the transaction is begun, committed, and rolled back on.</param>
/// <param name="logger">The logger rollbacks are written to.</param>
/// <exception cref="ArgumentNullException"><paramref name="unitOfWork"/> or <paramref name="logger"/> is <see langword="null"/>.</exception>
public sealed class TransactionBehavior<TRequest, TResponse>(
    IUnitOfWork unitOfWork,
    ILogger<TransactionBehavior<TRequest, TResponse>> logger
) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ITransactionalCommand<TResponse>
    where TResponse : IUnion, ITransactionOutcome<TResponse>
{
    private readonly IUnitOfWork _unitOfWork =
        unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> or <paramref name="next"/> is <see langword="null"/>.</exception>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken
    )
    {
        // Guard before BeginTransactionAsync so a null argument never opens a transaction.
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var response = await next(cancellationToken);

            if (TResponse.ShouldCommit(response))
            {
                await _unitOfWork.CommitAsync(cancellationToken);
            }
            else
            {
                _logger.LogInformation(
                    "{RequestName} produced {ResultCase}; rolling back transaction",
                    typeof(TRequest).Name,
                    response.Value?.GetType().Name ?? "null"
                );
                await _unitOfWork.RollbackAsync(cancellationToken);
            }

            return response;
        }
        catch (Exception ex)
        {
            // Log the request-specific rollback context here, then rethrow the original exception
            // unmodified (not wrapped) so its type and stack trace survive for the caller.
            _logger.LogError(
                ex,
                "{RequestName} threw; rolling back transaction",
                typeof(TRequest).Name
            );
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
