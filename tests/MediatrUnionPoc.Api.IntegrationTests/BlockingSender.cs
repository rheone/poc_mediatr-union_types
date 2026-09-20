using MediatR;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>An <see cref="ISender"/> whose <c>Send</c> blocks until the request is aborted, simulating a slow handler the client hangs up on.</summary>
internal sealed class BlockingSender : ISender
{
    /// <summary>Gets a source completed when a request reaches the sender.</summary>
    public TaskCompletionSource Started { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets a source completed once the blocked request has been cancelled.</summary>
    public TaskCompletionSource Finished { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <inheritdoc/>
    public async Task<TResponse> Send<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default
    )
    {
        Started.TrySetResult();
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException(
                "Unreachable: the delay only ends by cancellation."
            );
        }
        finally
        {
            Finished.TrySetResult();
        }
    }

    /// <inheritdoc/>
    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    /// <inheritdoc/>
    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamRequest<TResponse> request,
        CancellationToken cancellationToken = default
    ) => throw new NotSupportedException();

    /// <inheritdoc/>
    public IAsyncEnumerable<object?> CreateStream(
        object request,
        CancellationToken cancellationToken = default
    ) => throw new NotSupportedException();
}
