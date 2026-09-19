using MediatrUnionPoc.Application.Common.Results;

namespace MediatrUnionPoc.Application.Common.Abstractions;

/// <summary>
/// Lets <see cref="Behaviors.ValidationBehavior{TRequest,TResponse}"/> short-circuit generically:
/// every union response that can express a <see cref="ValidationErrors"/> case implements this,
/// so the pipeline can build one without knowing the concrete union type.
/// </summary>
/// <typeparam name="TSelf">The implementing union type itself.</typeparam>
public interface IValidatable<TSelf>
    where TSelf : IValidatable<TSelf>
{
    /// <summary>Builds this union's <c>ValidationErrors</c>-equivalent case from a validation failure.</summary>
    /// <param name="errors">The validation failures to represent.</param>
    /// <returns>An instance of <typeparamref name="TSelf"/> carrying <paramref name="errors"/>.</returns>
    static abstract TSelf FromValidationErrors(ValidationErrors errors);
}
