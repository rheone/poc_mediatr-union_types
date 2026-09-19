using MediatR;
using MediatrUnionPoc.Application.Common.Behaviors;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Features.Products.Create;
using MediatrUnionPoc.Application.Tests.TestData;
using Microsoft.Extensions.Logging;
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

/// <summary>A request whose response is a plain <see cref="string"/>, not a union.</summary>
/// <param name="Secret">A payload value that must never be written to the log.</param>
public sealed record PlainStringRequest(string Secret) : IRequest<string>;

/// <summary>
/// Verifies what <see cref="LoggingBehavior{TRequest,TResponse}"/> writes: an Information
/// "Handling" line before <c>next</c>, an Information "Handled" line naming the response's case
/// after it, and only type names as structured values, never request payloads.
/// </summary>
public sealed class LoggingBehaviorLoggingTests
{
    private const string ProductName = "Widget-Payload-Marker";
    private const decimal ProductPrice = 9.99m;
    private const string ErrorMessage = "boom";
    private const string ErrorCode = "BOOM";
    private const string SecretPayload = "top-secret-payload";
    private const string PlainResponse = "plain";
    private const string HandlingTemplate = "Handling {RequestName}";
    private const string HandledTemplate = "Handled {RequestName} -> {ResultCase}";
    private const string RequestNameKey = "RequestName";
    private const string ResultCaseKey = "ResultCase";
    private const string NullCaseName = "null";

    private readonly CapturingLogger<
        LoggingBehavior<CreateProductCommand, CreateProductResult>
    > _logger = new();

    /// <summary>Verifies a union response yields a "Handling" line then a "Handled" line naming the union's runtime case, both at Information.</summary>
    /// <returns>The asynchronous test operation.</returns>
    // Auto Generated, verify expected behavior:
    [Fact]
    public async Task Handle_UnionResponse_LogsHandlingThenHandledWithCaseName_Test()
    {
        // Arrange
        var sut = new LoggingBehavior<CreateProductCommand, CreateProductResult>(_logger);
        CreateProductResult response = new Error(ErrorMessage, ErrorCode);

        // Act
        await sut.Handle(
            new CreateProductCommand(ProductName, ProductPrice),
            _ => Task.FromResult(response),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Collection(
            _logger.Entries,
            handling =>
            {
                Assert.Equal(LogLevel.Information, handling.Level);
                Assert.Equal(HandlingTemplate, handling.Template);
                Assert.Equal($"Handling {nameof(CreateProductCommand)}", handling.Message);
                Assert.Equal(nameof(CreateProductCommand), handling.Properties[RequestNameKey]);
            },
            handled =>
            {
                Assert.Equal(LogLevel.Information, handled.Level);
                Assert.Equal(HandledTemplate, handled.Template);
                Assert.Equal(
                    $"Handled {nameof(CreateProductCommand)} -> {nameof(Error)}",
                    handled.Message
                );
                Assert.Equal(nameof(CreateProductCommand), handled.Properties[RequestNameKey]);
                Assert.Equal(nameof(Error), handled.Properties[ResultCaseKey]);
            }
        );
    }

    /// <summary>Verifies a non-union response is logged under its own runtime type name.</summary>
    /// <returns>The asynchronous test operation.</returns>
    // Auto Generated, verify expected behavior:
    [Fact]
    public async Task Handle_NonUnionResponse_LogsResponseTypeNameAsCase_Test()
    {
        // Arrange
        var logger = new CapturingLogger<LoggingBehavior<PlainStringRequest, string>>();
        var sut = new LoggingBehavior<PlainStringRequest, string>(logger);

        // Act
        await sut.Handle(
            new PlainStringRequest(SecretPayload),
            _ => Task.FromResult(PlainResponse),
            TestContext.Current.CancellationToken
        );

        // Assert
        var handled = logger.Entries[^1];
        Assert.Equal(LogLevel.Information, handled.Level);
        Assert.Equal(nameof(String), handled.Properties[ResultCaseKey]);
    }

    /// <summary>Verifies a null response is logged with the literal case name "null" rather than throwing.</summary>
    /// <returns>The asynchronous test operation.</returns>
    // Auto Generated, verify expected behavior:
    [Fact]
    public async Task Handle_NullResponse_LogsNullAsCase_Test()
    {
        // Arrange
        var logger = new CapturingLogger<LoggingBehavior<PlainStringRequest, string?>>();
        var sut = new LoggingBehavior<PlainStringRequest, string?>(logger);

        // Act
        await sut.Handle(
            new PlainStringRequest(SecretPayload),
            _ => Task.FromResult<string?>(null),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(NullCaseName, logger.Entries[^1].Properties[ResultCaseKey]);
    }

    /// <summary>Verifies that when <c>next</c> throws only the "Handling" line is written; no "Handled" line and no error log.</summary>
    /// <returns>The asynchronous test operation.</returns>
    // Auto Generated, verify expected behavior:
    [Fact]
    public async Task Handle_NextThrows_LogsOnlyHandlingLine_Test()
    {
        // Arrange
        var sut = new LoggingBehavior<CreateProductCommand, CreateProductResult>(_logger);

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.Handle(
                new CreateProductCommand(ProductName, ProductPrice),
                static Task<CreateProductResult> (_) => throw new InvalidOperationException(),
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(HandlingTemplate, entry.Template);
    }

    /// <summary>Verifies request payload values never appear in any log message or structured property; only type names do.</summary>
    /// <returns>The asynchronous test operation.</returns>
    // Auto Generated, verify expected behavior:
    [Fact]
    public async Task Handle_AnyRequest_DoesNotLogRequestPayload_Test()
    {
        // Arrange
        var sut = new LoggingBehavior<CreateProductCommand, CreateProductResult>(_logger);
        CreateProductResult response = new Error(ErrorMessage, ErrorCode);

        // Act
        await sut.Handle(
            new CreateProductCommand(ProductName, ProductPrice),
            _ => Task.FromResult(response),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.All(
            _logger.Entries,
            entry =>
            {
                Assert.DoesNotContain(ProductName, entry.Message, StringComparison.Ordinal);
                Assert.DoesNotContain(ErrorMessage, entry.Message, StringComparison.Ordinal);
                Assert.All(entry.Properties.Values, value => Assert.IsType<string>(value));
                Assert.DoesNotContain(ProductName, entry.Properties.Values);
                Assert.DoesNotContain(ErrorMessage, entry.Properties.Values);
            }
        );
    }
}
