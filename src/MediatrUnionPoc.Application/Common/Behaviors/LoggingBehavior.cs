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
