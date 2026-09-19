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
    private const string ValidName = "Widget";
    private const decimal ValidPrice = 10m;

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
    public async Task Handle_InvalidRequest_ShortCircuitsToValidationErrors_Test()
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
        var command = new CreateProductCommand(string.Empty, ValidPrice);

        // Act
        var result = await _sut.Handle(command, NextAsync, CancellationToken.None);

        // Assert
        Assert.False(_nextWasCalled);
        var errors = Assert.IsType<ValidationErrors>(((IUnion)result).Value);
        var error = Assert.Single(errors.Errors);
        Assert.Equal(InvalidPropertyName, error.PropertyName);
        Assert.Equal(InvalidPropertyMessage, error.ErrorMessage);
        await _validator
            .Received(1)
            .ValidateAsync(
                Arg.Is<ValidationContext<CreateProductCommand>>(context =>
                    ReferenceEquals(context.InstanceToValidate, command)
                ),
                Arg.Any<CancellationToken>()
            );
    }

    /// <summary>Verifies a passing <see cref="IValidator{T}"/> result lets the request reach <c>next</c>, returning its result unchanged.</summary>
    /// <returns>The asynchronous test operation.</returns>
    [Fact]
    public async Task Handle_ValidRequest_CallsNextAndReturnsResultUnchanged_Test()
    {
        // Arrange
        _validator
            .ValidateAsync(
                Arg.Any<ValidationContext<CreateProductCommand>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(new ValidationResult());
        var command = new CreateProductCommand(ValidName, ValidPrice);

        // Act
        var result = await _sut.Handle(command, NextAsync, CancellationToken.None);

        // Assert
        Assert.True(_nextWasCalled);
        Assert.IsType<Error>(((IUnion)result).Value);
        await _validator
            .Received(1)
            .ValidateAsync(
                Arg.Is<ValidationContext<CreateProductCommand>>(context =>
                    ReferenceEquals(context.InstanceToValidate, command)
                ),
                Arg.Any<CancellationToken>()
            );
    }

    /// <summary>Verifies <see cref="CreateProductCommand"/> deliberately does not guard a null name in its constructor, so the real <see cref="CreateProductValidator"/> reports the Name error through the behavior instead.</summary>
    /// <returns>The asynchronous test operation.</returns>
    // Auto Generated, verify expected behavior:
    [Fact]
    public async Task Ctor_NullName_DoesNotThrowAndFailsValidation_Test()
    {
        // Arrange
        var sut = new ValidationBehavior<CreateProductCommand, CreateProductResult>(
            [new CreateProductValidator()],
            NullLogger<ValidationBehavior<CreateProductCommand, CreateProductResult>>.Instance
        );

        // Act
        var command = new CreateProductCommand(null!, ValidPrice);
        var result = await sut.Handle(command, NextAsync, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(_nextWasCalled);
        var errors = Assert.IsType<ValidationErrors>(((IUnion)result).Value);
        Assert.Contains(errors.Errors, e => e.PropertyName == InvalidPropertyName);
    }

    /// <summary>Verifies a request with no registered validators skips validation entirely and reaches <c>next</c>.</summary>
    /// <returns>The asynchronous test operation.</returns>
    // Auto Generated, verify expected behavior:
    [Fact]
    public async Task Handle_NoRegisteredValidators_CallsNext_Test()
    {
        // Arrange
        var sut = new ValidationBehavior<CreateProductCommand, CreateProductResult>(
            [],
            NullLogger<ValidationBehavior<CreateProductCommand, CreateProductResult>>.Instance
        );
        var command = new CreateProductCommand(ValidName, ValidPrice);

        // Act
        var result = await sut.Handle(command, NextAsync, CancellationToken.None);

        // Assert
        Assert.True(_nextWasCalled);
        Assert.IsType<Error>(((IUnion)result).Value);
    }

    /// <summary>Verifies failures from every registered validator are aggregated into one <see cref="ValidationErrors"/>, not just the first validator's.</summary>
    /// <returns>The asynchronous test operation.</returns>
    // Auto Generated, verify expected behavior:
    [Fact]
    public async Task Handle_FailuresFromMultipleValidators_AreAggregated_Test()
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
        var command = new CreateProductCommand(string.Empty, -1m);

        // Act
        var result = await sut.Handle(command, NextAsync, CancellationToken.None);

        // Assert
        var errors = Assert.IsType<ValidationErrors>(((IUnion)result).Value);
        Assert.Equal(new[] { "Name", "Price" }, errors.Errors.Select(e => e.PropertyName).Order());
        await _validator
            .Received(1)
            .ValidateAsync(
                Arg.Is<ValidationContext<CreateProductCommand>>(context =>
                    ReferenceEquals(context.InstanceToValidate, command)
                ),
                Arg.Any<CancellationToken>()
            );
        await second
            .Received(1)
            .ValidateAsync(
                Arg.Is<ValidationContext<CreateProductCommand>>(context =>
                    ReferenceEquals(context.InstanceToValidate, command)
                ),
                Arg.Any<CancellationToken>()
            );
    }

    /// <summary>Stands in for the rest of the pipeline as a <see cref="RequestHandlerDelegate{TResponse}"/>, recording whether it was reached.</summary>
    private Task<CreateProductResult> NextAsync(CancellationToken cancellationToken = default)
    {
        _nextWasCalled = true;
        CreateProductResult sentinel = new Error("sentinel from next()", "SENTINEL");
        return Task.FromResult(sentinel);
    }

    /// <summary>Verifies the constructor rejects a null validators sequence instead of failing on first use.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void Ctor_NullValidators_ThrowsArgumentNullException_Test()
    {
        // Act
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new ValidationBehavior<CreateProductCommand, CreateProductResult>(
                null!,
                NullLogger<ValidationBehavior<CreateProductCommand, CreateProductResult>>.Instance
            )
        );

        // Assert
        Assert.Equal("validators", ex.ParamName);
    }

    /// <summary>Verifies the constructor rejects a null logger instead of failing on first use.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void Ctor_NullLogger_ThrowsArgumentNullException_Test()
    {
        // Act
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new ValidationBehavior<CreateProductCommand, CreateProductResult>([_validator], null!)
        );

        // Assert
        Assert.Equal("logger", ex.ParamName);
    }

    /// <summary>Verifies a null request is rejected with <see cref="ArgumentNullException"/> before any validator runs.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    // Auto Generated, verify expected behavior:
    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException_Test()
    {
        // Act
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _sut.Handle(null!, NextAsync, CancellationToken.None)
        );

        // Assert
        Assert.Equal("request", ex.ParamName);
        Assert.False(_nextWasCalled);
        await _validator
            .DidNotReceiveWithAnyArgs()
            .ValidateAsync(
                default(ValidationContext<CreateProductCommand>)!,
                TestContext.Current.CancellationToken
            );
    }

    /// <summary>Verifies a null <c>next</c> delegate is rejected with <see cref="ArgumentNullException"/> before any validator runs, even when validation would have passed.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    // Auto Generated, verify expected behavior:
    [Fact]
    public async Task Handle_NullNext_ThrowsArgumentNullException_Test()
    {
        // Act
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _sut.Handle(
                new CreateProductCommand(ValidName, ValidPrice),
                null!,
                CancellationToken.None
            )
        );

        // Assert
        Assert.Equal("next", ex.ParamName);
        await _validator
            .DidNotReceiveWithAnyArgs()
            .ValidateAsync(
                default(ValidationContext<CreateProductCommand>)!,
                TestContext.Current.CancellationToken
            );
    }
}
