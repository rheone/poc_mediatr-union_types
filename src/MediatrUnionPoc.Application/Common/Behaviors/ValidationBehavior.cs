using FluentValidation;
using MediatR;
using MediatrUnionPoc.Application.Common.Abstractions;
using MediatrUnionPoc.Application.Common.Results;
using Microsoft.Extensions.Logging;

namespace MediatrUnionPoc.Application.Common.Behaviors;

/// <summary>
/// Runs all registered FluentValidation validators for <typeparamref name="TRequest"/>.
/// On failure, short-circuits the pipeline by asking the union response itself (via the
/// static abstract factory on <see cref="IValidatable{TSelf}"/>) to build its <c>ValidationErrors</c>
/// case — the handler never runs and never throws for this, invalid input is simply another
/// outcome.
/// </summary>
/// <typeparam name="TRequest">The MediatR request type being validated.</typeparam>
/// <typeparam name="TResponse">The request's response union type, which must implement <see cref="IValidatable{TSelf}"/>.</typeparam>
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators,
    ILogger<ValidationBehavior<TRequest, TResponse>> logger
) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IValidatable<TResponse>
{
    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken
    )
    {
        if (!validators.Any())
        {
            // No validators registered for TRequest — skip building a ValidationContext and
            // running FluentValidation's async machinery for nothing.
            return await next(cancellationToken);
        }

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken))
        );

        var failures = results
            .SelectMany(r => r.Errors)
            .Select(f => new ValidationError(f.PropertyName, f.ErrorMessage))
            .ToList();

        if (failures.Count > 0)
        {
            logger.LogWarning(
                "Validation failed for {RequestName} with {Count} error(s)",
                typeof(TRequest).Name,
                failures.Count
            );

            return TResponse.FromValidationErrors(new ValidationErrors(failures));
        }

        return await next(cancellationToken);
    }
}
