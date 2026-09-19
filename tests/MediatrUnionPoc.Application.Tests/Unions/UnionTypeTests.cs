using System.Runtime.CompilerServices;
using MediatrUnionPoc.Application.Common.Abstractions;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Features.Products.Common;
using MediatrUnionPoc.Application.Features.Products.Create;
using MediatrUnionPoc.Application.Features.Products.Update;
using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Application.Tests.Unions;

/// <summary>
/// These tests exist purely to answer the POC's question: does the C# `union` feature behave
/// the way a MediatR CQRS response needs to? They don't re-test FluentValidation, MediatR, or EF
/// Core — only the union mechanics the rest of the codebase leans on: implicit conversion from
/// each case type, exhaustive pattern matching, exposing the boxed case via IUnion, and using a
/// union as a type argument to a generic, static-abstract-member constraint.
/// </summary>
public class UnionTypeTests
{
    private static readonly ProductId SomeProductId = ProductId.From(
        Guid.Parse("99999999-9999-9999-9999-999999999999")
    );

    /// <summary>
    /// Verifies each declared case type (<see cref="ProductDto"/>, <see cref="ValidationErrors"/>,
    /// <see cref="Error"/>) implicitly converts into <c>CreateProductResult</c> and is exposed,
    /// unboxed, through <see cref="IUnion.Value"/>.
    /// </summary>
    [Fact]
    public void Conversion_each_declared_case_type_converts_implicitly_into_the_union()
    {
        // Arrange
        var dto = new ProductDto(SomeProductId, "Widget", 9.99m);
        var validationErrors = new ValidationErrors([new ValidationError("Name", "required")]);
        var error = new Error("boom", "BOOM");

        // Act
        CreateProductResult fromDto = dto;
        CreateProductResult fromValidation = validationErrors;
        CreateProductResult fromError = error;

        // Assert
        Assert.Same(dto, ((IUnion)fromDto).Value);
        Assert.Same(validationErrors, ((IUnion)fromValidation).Value);
        Assert.Same(error, ((IUnion)fromError).Value);
    }

    /// <summary>
    /// Verifies a <c>switch</c> over the union pattern-matches directly on each case type, without
    /// any explicit unwrapping step.
    /// </summary>
    [Fact]
    public void Switch_pattern_matching_unwraps_to_the_contained_case_type()
    {
        // Arrange
        CreateProductResult result = new ProductDto(SomeProductId, "Widget", 9.99m);

        // Act
        var description = result switch
        {
            ProductDto dto => $"created:{dto.Name}",
            ValidationErrors => "invalid",
            Error => "error",
        };

        // Assert
        Assert.Equal("created:Widget", description);
    }

    /// <summary>
    /// Proves a case-complete <c>switch</c> over the union compiles with no discard arm — the
    /// runtime assertion is secondary; the compile itself is the guarantee under test.
    /// </summary>
    [Fact]
    public void Switch_over_every_declared_case_is_exhaustive_without_a_discard_arm()
    {
        // Arrange
        // This is a compile-time assertion as much as a runtime one: if CreateProductResult ever
        // gained or lost a case type, this switch would stop compiling (no discard arm) until
        // updated, which is exactly the guarantee the union is supposed to provide.
        static string Describe(CreateProductResult result) =>
            result switch
            {
                ProductDto dto => $"created:{dto.Name}",
                ValidationErrors errors => $"invalid:{errors.Errors.Count}",
                Error error => $"error:{error.Code}",
            };

        // Act
        var description = Describe(new Error("boom", "BOOM"));

        // Assert
        Assert.Equal("error:BOOM", description);
    }

    /// <summary>
    /// Verifies <c>UpdateProductResult</c>'s different case set (<see cref="Success"/>,
    /// <see cref="NotFound{TId}"/>, <see cref="ValidationErrors"/>, <see cref="Error"/>,
    /// <see cref="NotAuthorized"/>) works the same way as <c>CreateProductResult</c>'s, proving
    /// unions can be mixed-and-matched per endpoint rather than sharing one fixed "Result" shape.
    /// </summary>
    [Fact]
    public void Conversion_a_different_mix_of_case_types_works_on_another_union()
    {
        // Arrange
        // UpdateProductResult mixes Success/NotFound/ValidationErrors/Error/NotAuthorized — a
        // different case set from CreateProductResult's ProductDto/ValidationErrors/Error,
        // proving unions can be mixed-and-matched per endpoint rather than sharing one fixed
        // "Result" shape.
        UpdateProductResult success = new Success();
        UpdateProductResult notFound = new NotFound<ProductId>(SomeProductId);

        // Act
        var successDescription = success switch
        {
            Success => "ok",
            NotFound<ProductId> => "missing",
            ValidationErrors => "invalid",
            Error => "error",
            NotAuthorized => "unauthorized",
        };

        // Assert
        Assert.IsType<Success>(((IUnion)success).Value);
        Assert.IsType<NotFound<ProductId>>(((IUnion)notFound).Value);
        Assert.Equal("ok", successDescription);
    }

    /// <summary>
    /// Verifies generic code constrained only to <see cref="IValidatable{TSelf}"/> — with no
    /// knowledge of which concrete union <c>TResponse</c> is — can still build that union's
    /// <see cref="ValidationErrors"/> case via the static abstract factory member. This is the
    /// mechanism <c>ValidationBehavior&lt;TRequest,TResponse&gt;</c> depends on.
    /// </summary>
    [Fact]
    public void FromValidationErrors_generic_code_builds_the_ValidationErrors_case_via_the_static_abstract_factory()
    {
        // Arrange
        // This is what ValidationBehavior<TRequest,TResponse> relies on: given only the
        // constraint `TResponse : IValidatable<TResponse>`, generic code can build a concrete
        // union's ValidationErrors case without knowing which union TResponse actually is.
        var errors = new ValidationErrors([new ValidationError("Price", "must be >= 0")]);

        // Act
        var built = BuildFromValidationErrors<CreateProductResult>(errors);

        // Assert
        Assert.Same(errors, ((IUnion)built).Value);
    }

    private static TResponse BuildFromValidationErrors<TResponse>(ValidationErrors errors)
        where TResponse : IValidatable<TResponse> => TResponse.FromValidationErrors(errors);
}
