using System.Runtime.CompilerServices;
using MediatR;
using MediatrUnionPoc.Application.Common.Abstractions;
using MediatrUnionPoc.Domain;
using Microsoft.Extensions.Logging;

namespace MediatrUnionPoc.Application.Common.Behaviors;

/// <summary>
/// Wraps <see cref="ITransactionalCommand{TResponse}"/> execution in a unit-of-work transaction —
/// a plain <see cref="ICommand{TResponse}"/> that never opted into <see cref="ITransactionalCommand{TResponse}"/>
/// (e.g. one that only publishes an event, with no persistence to commit or roll back) simply
/// doesn't match this behavior's generic constraints and skips it entirely; that's a deliberate
/// opt-in, not every command needing a transaction. Commit-vs-rollback is decided by asking
/// the union itself — <see cref="ITransactionOutcome{TSelf}.ShouldCommit"/> — never by this
/// behavior inspecting which case type came back. Shared case types are meaning-free and reusable
/// across unions (an <c>Error</c> in one union might mean something entirely different in
/// another), so only the union that declares a case type gets to say what that case means for its own
/// transaction; this behavior stays generic over every command without knowing any of them.
/// Because each union's <c>ShouldCommit</c> implementation is a <c>switch</c> over its own closed
/// set of case types, the compiler forces every case — including ones added after this behavior
/// was written — to be classified. There is no default branch here to silently commit (or roll
/// back) an unrecognized case, because this behavior never sees case types at all.
/// No exceptions are used for this branching, since a domain-level failure (validation, not-found,
/// unauthorized, business-rule failure) is an ordinary, expected outcome, not an exceptional one.
/// A thrown exception still triggers a rollback, but is reserved for genuinely unexpected
/// infrastructure failures.
/// </summary>
/// <typeparam name="TRequest">The transactional command type.</typeparam>
/// <typeparam name="TResponse">The command's response union type.</typeparam>
public sealed class TransactionBehavior<TRequest, TResponse>(
    IUnitOfWork unitOfWork,
    ILogger<TransactionBehavior<TRequest, TResponse>> logger
) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ITransactionalCommand<TResponse>
    where TResponse : IUnion, ITransactionOutcome<TResponse>
{
    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken
    )
    {
        await unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var response = await next(cancellationToken);

            if (TResponse.ShouldCommit(response))
            {
                await unitOfWork.CommitAsync(cancellationToken);
            }
            else
            {
                logger.LogInformation(
                    "{RequestName} produced {ResultCase}; rolling back transaction",
                    typeof(TRequest).Name,
                    response.Value?.GetType().Name ?? "null"
                );
                await unitOfWork.RollbackAsync(cancellationToken);
            }

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "{RequestName} threw; rolling back transaction",
                typeof(TRequest).Name
            );
            await unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
