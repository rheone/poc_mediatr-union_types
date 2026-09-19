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
    private const string ValidName = "Widget";
    private const decimal ValidPrice = 10m;
    private const int MaxNameLength = 200;

    private readonly CreateProductValidator _sut = new();

    /// <summary>Verifies a null, empty, or whitespace-only name fails validation.</summary>
    /// <param name="name">A null, empty, or whitespace-only name value that should fail validation.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData("\r")]
    public void Validate_null_empty_or_whitespace_name_fails(string? name)
    {
        // Arrange
        var command = new CreateProductCommand(name!, ValidPrice);

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    /// <summary>Verifies a name over 200 characters fails validation.</summary>
    [Fact]
    public void Validate_name_over_200_characters_fails()
    {
        // Arrange
        var command = new CreateProductCommand(new string('a', MaxNameLength + 1), ValidPrice);

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    // Auto Generated, verify expected behavior:
    /// <summary>Verifies a name of exactly 200 characters is accepted — the upper boundary of the length rule.</summary>
    [Fact]
    public void Validate_name_of_exactly_200_characters_passes()
    {
        // Arrange
        var command = new CreateProductCommand(new string('a', MaxNameLength), ValidPrice);

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    /// <summary>Verifies a negative price fails validation.</summary>
    /// <param name="price">A negative price value that should fail validation.</param>
    [Theory]
    [InlineData(-0.01)]
    [InlineData(-1000)]
    public void Validate_negative_price_fails(decimal price)
    {
        // Arrange
        var command = new CreateProductCommand(ValidName, price);

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Price);
    }

    /// <summary>Verifies a fully valid command produces no validation errors.</summary>
    /// <param name="price">A valid, non-negative price value.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(0.01)]
    [InlineData(9999.99)]
    public void Validate_valid_command_produces_no_errors(decimal price)
    {
        // Arrange
        var command = new CreateProductCommand(ValidName, price);

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
