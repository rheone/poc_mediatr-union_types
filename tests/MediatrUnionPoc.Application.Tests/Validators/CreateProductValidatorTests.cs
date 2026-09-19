using FluentValidation.TestHelper;
using MediatrUnionPoc.Application.Features.Products.Create;

namespace MediatrUnionPoc.Application.Tests.Validators;

/// <summary>
/// Exercises <see cref="CreateProductValidator"/>'s actual FluentValidation rules directly — the
/// pipeline tests substitute <c>IValidator&lt;T&gt;</c> entirely, so nothing else in this suite
/// proves these specific rules (empty name, name length, negative price) are wired correctly.
/// </summary>
public class CreateProductValidatorTests
{
    private readonly CreateProductValidator _sut = new();

    /// <summary>Verifies an empty or whitespace-only name fails validation.</summary>
    /// <param name="name">An empty or whitespace-only name value that should fail validation.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Name_must_not_be_empty_or_whitespace(string name)
    {
        var result = _sut.TestValidate(new CreateProductCommand(name, 10m));

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    /// <summary>Verifies a name over 200 characters fails validation.</summary>
    [Fact]
    public void Name_must_not_exceed_200_characters()
    {
        var result = _sut.TestValidate(new CreateProductCommand(new string('a', 201), 10m));

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    /// <summary>Verifies a negative price fails validation.</summary>
    /// <param name="price">A negative price value that should fail validation.</param>
    [Theory]
    [InlineData(-0.01)]
    [InlineData(-1000)]
    public void Price_must_not_be_negative(decimal price)
    {
        var result = _sut.TestValidate(new CreateProductCommand("Widget", price));

        result.ShouldHaveValidationErrorFor(x => x.Price);
    }

    /// <summary>Verifies a fully valid command produces no validation errors.</summary>
    /// <param name="price">A valid, non-negative price value.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(0.01)]
    [InlineData(9999.99)]
    public void Valid_commands_produce_no_errors(decimal price)
    {
        var result = _sut.TestValidate(new CreateProductCommand("Widget", price));

        result.ShouldNotHaveAnyValidationErrors();
    }
}
