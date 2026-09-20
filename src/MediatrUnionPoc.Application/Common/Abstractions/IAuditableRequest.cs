using System.Runtime.CompilerServices;
using System.Security.Claims;
using MediatrUnionPoc.Application.Common.Auditing;

namespace MediatrUnionPoc.Application.Common.Abstractions;

/// <summary>
/// A request whose every outcome <see cref="Behaviors.AuditBehavior{TRequest,TResponse}"/> records in
/// the audit stream, including the outcomes the authorization and validation behaviors short-circuit
/// to. Opting in is deliberate per request. The request, not the behavior, knows what its own response
/// union means, so the target is learned by asking the request to describe the response: case types
/// stay meaning-free and the behavior never inspects them, it only reads the runtime case name for
/// the outcome.
/// </summary>
/// <typeparam name="TResponse">The request's response union type.</typeparam>
public interface IAuditableRequest<TResponse>
    where TResponse : IUnion
{
    /// <summary>Gets the stable dotted action name recorded for this request, such as <c>Product.Create</c>.</summary>
    string AuditAction { get; }

    /// <summary>Gets what happens when the event cannot be written; see <see cref="Auditing.AuditFailurePolicy"/>.</summary>
    AuditFailurePolicy AuditFailurePolicy { get; }

    /// <summary>Gets the caller the event is attributed to, or <see langword="null"/> when the request carries none.</summary>
    ClaimsPrincipal? AuditPrincipal { get; }

    /// <summary>Describes the target, reason and extra facts of this request given the response it produced.</summary>
    /// <param name="response">The response the rest of the pipeline returned (possibly a short-circuited authorization or validation outcome).</param>
    /// <returns>The description; never a token, secret or request body.</returns>
    AuditDescription DescribeAudit(TResponse response);

    /// <summary>Describes this request when the rest of the pipeline threw instead of returning a response. Defaults to nothing beyond who and what.</summary>
    /// <returns>The description.</returns>
    AuditDescription DescribeUnhandledAudit() => AuditDescription.None;
}
