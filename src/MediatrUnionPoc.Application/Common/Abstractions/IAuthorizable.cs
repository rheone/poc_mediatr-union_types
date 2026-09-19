using MediatrUnionPoc.Application.Common.Results;

namespace MediatrUnionPoc.Application.Common.Abstractions;

/// <summary>
/// Lets <see cref="Behaviors.AuthorizationBehavior{TRequest,TResponse}"/> short-circuit
/// generically: every union response that can express a <see cref="NotAuthorized"/> case
/// implements this, so the pipeline can build one without knowing the concrete union type — the
/// same role <see cref="IValidatable{TSelf}"/> plays for <see cref="ValidationErrors"/>.
/// </summary>
/// <typeparam name="TSelf">The implementing union type itself.</typeparam>
public interface IAuthorizable<TSelf>
    where TSelf : IAuthorizable<TSelf>
{
    /// <summary>Builds this union's <c>NotAuthorized</c> case from an authorization failure.</summary>
    /// <param name="notAuthorized">The authorization failure to represent.</param>
    /// <returns>An instance of <typeparamref name="TSelf"/> carrying <paramref name="notAuthorized"/>.</returns>
    static abstract TSelf FromNotAuthorized(NotAuthorized notAuthorized);
}
