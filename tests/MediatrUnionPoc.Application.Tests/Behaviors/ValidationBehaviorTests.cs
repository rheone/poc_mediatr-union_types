using System.Runtime.CompilerServices;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using MediatrUnionPoc.Application.Common.Behaviors;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Features.Products.Create;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace MediatrUnionPoc.Application.Tests.Behaviors;

/// <summary>Verifies invalid requests short-circuit to the union's ValidationErrors case without reaching the handler, and valid ones pass through unchanged.</summary>
public class ValidationBehaviorTests
{
    private const string InvalidPropertyName = "Name";
    private const string InvalidPropertyMessage = "Name is required";

    private readonly ValidationBehavior<CreateProductCommand, CreateProductResult> _sut;
    private readonly IValidator<CreateProductCommand> _validator;
    private bool _nextWasCalled;

    /// <summary>Wires up <see cref="_sut"/> against the substituted <see cref="_validator"/>.</summary>
    public ValidationBehaviorTests()
    {
        _validator = Substitute.For<IValidator<CreateProductCommand>>();
        _sut = new ValidationBehavior<CreateProductCommand, CreateProductResult>(
            [_validator],
            NullLogger<ValidationBehavior<CreateProductCommand, CreateProductResult>>.Instance
        );
    }

    /// <summary>Verifies a failing <see cref="IValidator{T}"/> result short-circuits to the <see cref="ValidationErrors"/> case, that <c>next</c> is never invoked, and that the failures surface unchanged.</summary>
    /// <returns>The asynchronous test operation.</returns>
    [Fact]
    public async Task Handle_invalid_request_short_circuits_to_ValidationErrors_without_calling_next()
    {
        // Arrange
        _validator
            .ValidateAsync(
                Arg.Any<ValidationContext<CreateProductCommand>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                new ValidationResult([
                    new ValidationFailure(InvalidPropertyName, InvalidPropertyMessage),
                ])
            );
        var command = new CreateProductCommand(string.Empty, 10m);

        // Act
        var result = await _sut.Handle(command, NextAsync, CancellationToken.None);

        // Assert
        Assert.False(_nextWasCalled);
        var errors = Assert.IsType<ValidationErrors>(((IUnion)result).Value);
        var error = Assert.Single(errors.Errors);
        Assert.Equal(InvalidPropertyName, error.PropertyName);
        Assert.Equal(InvalidPropertyMessage, error.ErrorMessage);
    }

    /// <summary>Verifies a passing <see cref="IValidator{T}"/> result lets the request reach <c>next</c>, returning its result unchanged.</summary>
    /// <returns>The asynchronous test operation.</returns>
    [Fact]
    public async Task Handle_valid_request_calls_next_and_returns_its_result_unchanged()
    {
        // Arrange
        _validator
            .ValidateAsync(
                Arg.Any<ValidationContext<CreateProductCommand>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(new ValidationResult());
        var command = new CreateProductCommand("Widget", 10m);

        // Act
        var result = await _sut.Handle(command, NextAsync, CancellationToken.None);

        // Assert
        Assert.True(_nextWasCalled);
        Assert.IsType<Error>(((IUnion)result).Value);
    }

    // Auto Generated, verify expected behavior:
    /// <summary>Verifies a request with no registered validators skips validation entirely and reaches <c>next</c>.</summary>
    /// <returns>The asynchronous test operation.</returns>
    [Fact]
    public async Task Handle_no_registered_validators_calls_next()
    {
        // Arrange
        var sut = new ValidationBehavior<CreateProductCommand, CreateProductResult>(
            [],
            NullLogger<ValidationBehavior<CreateProductCommand, CreateProductResult>>.Instance
        );
        var command = new CreateProductCommand("Widget", 10m);

        // Act
        var result = await sut.Handle(command, NextAsync, CancellationToken.None);

        // Assert
        Assert.True(_nextWasCalled);
        Assert.IsType<Error>(((IUnion)result).Value);
    }

    // Auto Generated, verify expected behavior:
    /// <summary>Verifies failures from every registered validator are aggregated into one <see cref="ValidationErrors"/>, not just the first validator's.</summary>
    /// <returns>The asynchronous test operation.</returns>
    [Fact]
    public async Task Handle_failures_from_multiple_validators_are_aggregated()
    {
        // Arrange
        var second = Substitute.For<IValidator<CreateProductCommand>>();
        _validator
            .ValidateAsync(
                Arg.Any<ValidationContext<CreateProductCommand>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(new ValidationResult([new ValidationFailure("Name", "bad name")]));
        second
            .ValidateAsync(
                Arg.Any<ValidationContext<CreateProductCommand>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(new ValidationResult([new ValidationFailure("Price", "bad price")]));
        var sut = new ValidationBehavior<CreateProductCommand, CreateProductResult>(
            [_validator, second],
            NullLogger<ValidationBehavior<CreateProductCommand, CreateProductResult>>.Instance
        );

        // Act
        var result = await sut.Handle(
            new CreateProductCommand(string.Empty, -1m),
            NextAsync,
            CancellationToken.None
        );

        // Assert
        var errors = Assert.IsType<ValidationErrors>(((IUnion)result).Value);
        Assert.Equal(
            ["Name", "Price"],
            errors.Errors.Select(e => e.PropertyName).Order().ToArray()
        );
    }

    /// <summary>Stands in for the rest of the pipeline as a <see cref="RequestHandlerDelegate{TResponse}"/>, recording whether it was reached.</summary>
    private Task<CreateProductResult> NextAsync(CancellationToken cancellationToken = default)
    {
        _nextWasCalled = true;
        CreateProductResult sentinel = new Error("sentinel from next()", "SENTINEL");
        return Task.FromResult(sentinel);
    }
}
