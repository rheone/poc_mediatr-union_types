using System.Runtime.CompilerServices;
using MediatR;
using Microsoft.Extensions.Logging;

namespace MediatrUnionPoc.Application.Common.Behaviors;

/// <summary>Logs every request and, for union responses, which case type came back.</summary>
/// <typeparam name="TRequest">The MediatR request type.</typeparam>
/// <typeparam name="TResponse">The request's response type.</typeparam>
public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger
) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken
    )
    {
        var requestName = typeof(TRequest).Name;
        logger.LogInformation("Handling {RequestName}", requestName);

        var response = await next(cancellationToken);

        // This behavior is generic over every MediatR request, not just union-returning ones, so
        // it can't assume TResponse is a union. When it is, log the case's own runtime type (e.g.
        // "NotFound") rather than the union's declared type, which would be the same for every
        // case and tell a reader nothing. Non-union responses fall back to their own type name.
        var caseName = response switch
        {
            IUnion union => union.Value?.GetType().Name ?? "null",
            null => "null",
            _ => response.GetType().Name,
        };

        logger.LogInformation("Handled {RequestName} -> {ResultCase}", requestName, caseName);

        return response;
    }
}
