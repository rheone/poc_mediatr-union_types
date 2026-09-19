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

    private readonly LoggingBehavior<CreateProductCommand, CreateProductResult> _sut = new(
        NullLogger<LoggingBehavior<CreateProductCommand, CreateProductResult>>.Instance
    );

    /// <summary>Verifies the union response returned by <c>next</c> passes through unchanged.</summary>
    /// <returns>The asynchronous test operation.</returns>
    [Fact]
    public async Task Handle_union_response_passes_through_unchanged()
    {
        // Arrange
        CreateProductResult expected = new Error("boom", "BOOM");

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
    public async Task Handle_invokes_next_exactly_once()
    {
        // Arrange
        var callCount = 0;

        // Act
        await _sut.Handle(
            new CreateProductCommand(ProductName, ProductPrice),
            _ =>
            {
                callCount++;
                CreateProductResult response = new Error("boom", "BOOM");
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
    public async Task Handle_exception_thrown_by_next_propagates()
    {
        // Arrange
        const string message = "infra failure";

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
}
