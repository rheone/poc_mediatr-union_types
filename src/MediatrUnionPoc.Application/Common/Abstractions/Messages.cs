using System.Runtime.CompilerServices;
using MediatR;

namespace MediatrUnionPoc.Application.Common.Abstractions;

/// <summary>
/// A request that mutates state, with no assumption about how — or whether — that mutation needs
/// a transaction. Not every command does: one that only publishes an event, or that never touches
/// persistence, is a plain <see cref="ICommand{TResponse}"/>. A command whose mutation should be
/// wrapped in a unit-of-work transaction is <see cref="ITransactionalCommand{TResponse}"/> instead.
/// </summary>
/// <typeparam name="TResponse">The command's response union type.</typeparam>
public interface ICommand<TResponse> : IRequest<TResponse>;

/// <summary>
/// A command whose result <see cref="Behaviors.TransactionBehavior{TRequest,TResponse}"/> should
/// commit or roll back — the only reason <typeparamref name="TResponse"/> must implement
/// <see cref="ITransactionOutcome{TSelf}"/>, enforced here at the command declaration so a command
/// whose union hasn't classified its own commit/rollback cases fails to compile rather than
/// silently skipping the transaction behavior at runtime (MediatR would otherwise just decline to
/// construct a behavior whose generic constraints aren't satisfied, with nothing to say why).
/// </summary>
/// <typeparam name="TResponse">The command's response union type, which must implement <see cref="ITransactionOutcome{TSelf}"/>.</typeparam>
public interface ITransactionalCommand<TResponse> : ICommand<TResponse>
    where TResponse : IUnion, ITransactionOutcome<TResponse>;

/// <summary>A read-only request.</summary>
/// <typeparam name="TResponse">The query's response union type.</typeparam>
public interface IQuery<TResponse> : IRequest<TResponse>;
