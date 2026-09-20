using System.Runtime.CompilerServices;
using MediatR;
using Microsoft.Extensions.Logging;

namespace MediatrUnionPoc.Application.Common.Behaviors;

/// <summary>
/// Logs every request and, for union responses, which case type came back. The two lines are
/// source-generated <c>[LoggerMessage]</c> methods (event ids 1000 and 1001) with the structured
/// properties <c>RequestName</c> and <c>ResultCase</c>.
/// </summary>
/// <typeparam name="TRequest">The MediatR request type.</typeparam>
/// <typeparam name="TResponse">The request's response type.</typeparam>
/// <param name="logger">The logger request/response lines are written to.</param>
/// <exception cref="ArgumentNullException"><paramref name="logger"/> is <see langword="null"/>.</exception>
public sealed partial class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger
) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger =
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

        var requestName = typeof(TRequest).Name;
        LogHandling(requestName);

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

        LogHandled(requestName, caseName);

        return response;
    }

    // Event ids are stable identifiers for filtering and alerting: 1000 to 1099 belong to this
    // behavior. Never renumber one; retire it and take the next free number.
    [LoggerMessage(
        EventId = 1000,
        EventName = "RequestHandling",
        Level = LogLevel.Information,
        Message = "Handling {RequestName}"
    )]
    private partial void LogHandling(string requestName);

    [LoggerMessage(
        EventId = 1001,
        EventName = "RequestHandled",
        Level = LogLevel.Information,
        Message = "Handled {RequestName} -> {ResultCase}"
    )]
    private partial void LogHandled(string requestName, string resultCase);
}
