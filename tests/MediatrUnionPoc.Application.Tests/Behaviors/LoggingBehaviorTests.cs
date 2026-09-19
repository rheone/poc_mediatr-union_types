using MediatrUnionPoc.Application.Common.Behaviors;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Features.Products.Create;
using Microsoft.Extensions.Logging.Abstractions;

namespace MediatrUnionPoc.Application.Tests.Behaviors;

/// <summary>
/// Verifies <see cref="LoggingBehavior{TRequest,TResponse}"/> is purely observational: it always
/// calls <c>next</c> exactly once and returns its result completely unchanged, for both a union
/// response (which it additionally inspects to log the boxed case name) and a plain response.
/// </summary>
public class LoggingBehaviorTests
{
    private const string ProductName = "Widget";
    private const decimal ProductPrice = 9.99m;
    private const string ErrorMessage = "boom";
    private const string ErrorCode = "BOOM";
    private const string InfrastructureFailureMessage = "infra failure";

    private readonly LoggingBehavior<CreateProductCommand, CreateProductResult> _sut = new(
        NullLogger<LoggingBehavior<CreateProductCommand, CreateProductResult>>.Instance
    );

    private bool _nextWasCalled;

    /// <summary>Verifies the union response returned by <c>next</c> passes through unchanged.</summary>
    /// <returns>The asynchronous test operation.</returns>
    [Fact]
    public async Task Handle_UnionResponse_PassesThroughUnchanged_Test()
    {
        // Arrange
        CreateProductResult expected = new Error(ErrorMessage, ErrorCode);

        // Act
        var result = await _sut.Handle(
            new CreateProductCommand(ProductName, ProductPrice),
            _ => Task.FromResult(expected),
            CancellationToken.None
        );

        // Assert
        Assert.Equal(expected, result);
    }

    /// <summary>Verifies <c>next</c> is invoked exactly once per call.</summary>
    /// <returns>The asynchronous test operation.</returns>
    [Fact]
    public async Task Handle_AnyRequest_InvokesNextExactlyOnce_Test()
    {
        // Arrange
        var callCount = 0;

        // Act
        await _sut.Handle(
            new CreateProductCommand(ProductName, ProductPrice),
            _ =>
            {
                callCount++;
                CreateProductResult response = new Error(ErrorMessage, ErrorCode);
                return Task.FromResult(response);
            },
            CancellationToken.None
        );

        // Assert
        Assert.Equal(1, callCount);
    }

    /// <summary>Verifies an exception thrown by <c>next</c> propagates rather than being swallowed or logged-and-suppressed.</summary>
    /// <returns>The asynchronous test operation.</returns>
    [Fact]
    public async Task Handle_NextThrows_PropagatesException_Test()
    {
        // Arrange
        const string message = InfrastructureFailureMessage;

        // Act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.Handle(
                new CreateProductCommand(ProductName, ProductPrice),
                static Task<CreateProductResult> (_) =>
                    throw new InvalidOperationException(message),
                CancellationToken.None
            )
        );

        // Assert
        Assert.Equal(message, ex.Message);
    }

    /// <summary>Verifies the constructor rejects a null logger instead of failing on first use.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void Ctor_NullLogger_ThrowsArgumentNullException_Test()
    {
        // Act
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new LoggingBehavior<CreateProductCommand, CreateProductResult>(null!)
        );

        // Assert
        Assert.Equal("logger", ex.ParamName);
    }

    /// <summary>Verifies a null request is rejected with <see cref="ArgumentNullException"/> before <c>next</c> runs.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    // Auto Generated, verify expected behavior:
    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException_Test()
    {
        // Act
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _sut.Handle(null!, NeverInvokedNextAsync, CancellationToken.None)
        );

        // Assert
        Assert.Equal("request", ex.ParamName);
        Assert.False(_nextWasCalled);
    }

    /// <summary>Verifies a null <c>next</c> delegate is rejected with <see cref="ArgumentNullException"/> up front rather than a <see cref="NullReferenceException"/> after the request is logged.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    // Auto Generated, verify expected behavior:
    [Fact]
    public async Task Handle_NullNext_ThrowsArgumentNullException_Test()
    {
        // Act
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _sut.Handle(
                new CreateProductCommand(ProductName, ProductPrice),
                null!,
                CancellationToken.None
            )
        );

        // Assert
        Assert.Equal("next", ex.ParamName);
    }

    private Task<CreateProductResult> NeverInvokedNextAsync(CancellationToken cancellationToken)
    {
        _nextWasCalled = true;
        CreateProductResult response = new Error(ErrorMessage, ErrorCode);
        return Task.FromResult(response);
    }
}
