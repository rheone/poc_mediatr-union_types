using System.Runtime.CompilerServices;
using MediatR;
using MediatrUnionPoc.Application.Common.Abstractions;
using MediatrUnionPoc.Application.Common.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace MediatrUnionPoc.Application.Common.Behaviors;

/// <summary>
/// Gates <see cref="IRequiresAuthorization"/> requests behind whichever policy
/// <see cref="IRequiresAuthorization.PolicyName"/> names before they reach their handler. On
/// failure, short-circuits the pipeline by asking the
/// union response itself (via the static abstract factory on <see cref="IAuthorizable{TSelf}"/>)
/// to build its <c>NotAuthorized</c> case — the handler never runs and never throws for this,
/// an unauthorized caller is simply another outcome.
/// </summary>
/// <typeparam name="TRequest">The MediatR request type being authorized.</typeparam>
/// <typeparam name="TResponse">The request's response union type, which must implement <see cref="IAuthorizable{TSelf}"/>.</typeparam>
/// <param name="authorizationService">The framework service the request's policy is evaluated with.</param>
/// <param name="logger">The logger denials are written to.</param>
/// <exception cref="ArgumentNullException"><paramref name="authorizationService"/> or <paramref name="logger"/> is <see langword="null"/>.</exception>
public sealed class AuthorizationBehavior<TRequest, TResponse>(
    IAuthorizationService authorizationService,
    ILogger<AuthorizationBehavior<TRequest, TResponse>> logger
) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, IRequiresAuthorization
    where TResponse : IUnion, IAuthorizable<TResponse>
{
    private readonly IAuthorizationService _authorizationService =
        authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
    private readonly ILogger<AuthorizationBehavior<TRequest, TResponse>> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> or <paramref name="next"/> is <see langword="null"/>.</exception>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        var authorizationResult = await _authorizationService.AuthorizeAsync(
            request.Principal,
            request.PolicyName
        );

        if (!authorizationResult.Succeeded)
        {
            _logger.LogWarning(
                "{RequestName} denied: caller does not satisfy the {Policy} policy",
                typeof(TRequest).Name,
                request.PolicyName
            );

            return TResponse.FromNotAuthorized(
                new NotAuthorized([$"The caller does not satisfy the {request.PolicyName} policy."])
            );
        }

        return await next(cancellationToken);
    }
}
