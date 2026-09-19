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
    public async Task Invalid_request_short_circuits_to_the_ValidationErrors_case_without_calling_next()
    {
        _validator
            .ValidateAsync(
                Arg.Any<ValidationContext<CreateProductCommand>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(new ValidationResult([new ValidationFailure("Name", "Name is required")]));

        var command = new CreateProductCommand(string.Empty, 10m);

        var result = await _sut.Handle(command, NextAsync, CancellationToken.None);

        Assert.False(_nextWasCalled);
        var errors = Assert.IsType<ValidationErrors>(
            ((System.Runtime.CompilerServices.IUnion)result).Value
        );
        Assert.Contains(errors.Errors, e => e.PropertyName == "Name");
    }

    /// <summary>Verifies a passing <see cref="IValidator{T}"/> result lets the request reach <c>next</c>, returning its result unchanged.</summary>
    /// <returns>The asynchronous test operation.</returns>
    [Fact]
    public async Task Valid_request_calls_next_and_returns_its_result_unchanged()
    {
        _validator
            .ValidateAsync(
                Arg.Any<ValidationContext<CreateProductCommand>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(new ValidationResult());

        var command = new CreateProductCommand("Widget", 10m);

        var result = await _sut.Handle(command, NextAsync, CancellationToken.None);

        Assert.True(_nextWasCalled);
        Assert.IsType<Error>(((System.Runtime.CompilerServices.IUnion)result).Value);
    }

    /// <summary>Stands in for the rest of the pipeline as a <see cref="RequestHandlerDelegate{TResponse}"/>, recording whether it was reached.</summary>
    private Task<CreateProductResult> NextAsync(CancellationToken cancellationToken = default)
    {
        _nextWasCalled = true;
        CreateProductResult sentinel = new Error("sentinel from next()", "SENTINEL");
        return Task.FromResult(sentinel);
    }
}
