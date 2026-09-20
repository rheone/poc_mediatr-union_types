using MediatR;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>An <see cref="ISender"/> whose every call throws the supplied exception, to force an unhandled failure through the real HTTP pipeline.</summary>
/// <param name="exception">The exception every call throws.</param>
internal sealed class ThrowingSender(Exception exception) : ISender
{
    /// <inheritdoc/>
    public Task<TResponse> Send<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default
    ) => throw exception;

    /// <inheritdoc/>
    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest => throw exception;

    /// <inheritdoc/>
    public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
        throw exception;

    /// <inheritdoc/>
    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamRequest<TResponse> request,
        CancellationToken cancellationToken = default
    ) => throw exception;

    /// <inheritdoc/>
    public IAsyncEnumerable<object?> CreateStream(
        object request,
        CancellationToken cancellationToken = default
    ) => throw exception;
}
