using System.Runtime.CompilerServices;
using MediatR;
using MediatrUnionPoc.Application.Common.Abstractions;
using MediatrUnionPoc.Application.Common.Authorization;
using MediatrUnionPoc.Application.Common.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace MediatrUnionPoc.Application.Common.Behaviors;

/// <summary>
/// Gates <see cref="IRequiresAdministrator"/> requests behind the
/// <see cref="AuthorizationPolicies.Administrator"/> policy before they reach their handler. On
/// failure, short-circuits the pipeline by asking the
/// union response itself (via the static abstract factory on <see cref="IAuthorizable{TSelf}"/>)
/// to build its <c>NotAuthorized</c> case — the handler never runs and never throws for this,
/// an unauthorized caller is simply another outcome.
/// </summary>
/// <typeparam name="TRequest">The MediatR request type being authorized.</typeparam>
/// <typeparam name="TResponse">The request's response union type, which must implement <see cref="IAuthorizable{TSelf}"/>.</typeparam>
public sealed class AuthorizationBehavior<TRequest, TResponse>(
    IAuthorizationService authorizationService,
    ILogger<AuthorizationBehavior<TRequest, TResponse>> logger
) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, IRequiresAdministrator
    where TResponse : IUnion, IAuthorizable<TResponse>
{
    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken
    )
    {
        var authorizationResult = await authorizationService.AuthorizeAsync(
            request.Principal,
            AuthorizationPolicies.Administrator
        );

        if (!authorizationResult.Succeeded)
        {
            logger.LogWarning(
                "{RequestName} denied: caller does not satisfy the {Policy} policy",
                typeof(TRequest).Name,
                AuthorizationPolicies.Administrator
            );

            return TResponse.FromNotAuthorized(
                new NotAuthorized(["The caller is not an administrator."])
            );
        }

        return await next(cancellationToken);
    }
}
