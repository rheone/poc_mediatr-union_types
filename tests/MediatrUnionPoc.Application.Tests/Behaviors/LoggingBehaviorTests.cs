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
    /// <summary>Verifies the union response returned by <c>next</c> passes through unchanged, whichever case it is.</summary>
    /// <returns>The asynchronous test operation.</returns>
    [Fact]
    public async Task Passes_through_a_union_response_unchanged()
    {
        var sut = new LoggingBehavior<CreateProductCommand, CreateProductResult>(
            NullLogger<LoggingBehavior<CreateProductCommand, CreateProductResult>>.Instance
        );
        CreateProductResult expected = new Error("boom", "BOOM");

        var result = await sut.Handle(
            new CreateProductCommand("Widget", 9.99m),
            _ => Task.FromResult(expected),
            CancellationToken.None
        );

        Assert.Equal(expected, result);
    }

    /// <summary>Verifies <c>next</c> is invoked exactly once per call.</summary>
    /// <returns>The asynchronous test operation.</returns>
    [Fact]
    public async Task Calls_next_exactly_once()
    {
        var sut = new LoggingBehavior<CreateProductCommand, CreateProductResult>(
            NullLogger<LoggingBehavior<CreateProductCommand, CreateProductResult>>.Instance
        );
        var callCount = 0;

        await sut.Handle(
            new CreateProductCommand("Widget", 9.99m),
            _ =>
            {
                callCount++;
                CreateProductResult response = new Error("boom", "BOOM");
                return Task.FromResult(response);
            },
            CancellationToken.None
        );

        Assert.Equal(1, callCount);
    }

    /// <summary>Verifies an exception thrown by <c>next</c> propagates rather than being swallowed or logged-and-suppressed.</summary>
    /// <returns>The asynchronous test operation.</returns>
    [Fact]
    public Task Propagates_an_exception_thrown_by_next()
    {
        var sut = new LoggingBehavior<CreateProductCommand, CreateProductResult>(
            NullLogger<LoggingBehavior<CreateProductCommand, CreateProductResult>>.Instance
        );

        return Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.Handle(
                new CreateProductCommand("Widget", 9.99m),
                static Task<CreateProductResult> (_) =>
                    throw new InvalidOperationException("infra failure"),
                CancellationToken.None
            )
        );
    }
}
