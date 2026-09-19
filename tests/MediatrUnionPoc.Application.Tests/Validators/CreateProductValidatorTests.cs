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
    // SWEEP-AMBIGUITY: validating a null command throws InvalidOperationException ("Cannot pass null model to
    // Validate") from FluentValidation rather than ArgumentNullException / a null command should throw
    // ArgumentNullException naming "instance", but no such test is written because production does not do that.
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
    public void Validate_NullEmptyOrWhitespaceName_FailsNameRule_Test(string? name)
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
    public void Validate_NameOver200Characters_FailsNameRule_Test()
    {
        // Arrange
        var command = new CreateProductCommand(new string('a', MaxNameLength + 1), ValidPrice);

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    /// <summary>Verifies a name of exactly 200 characters is accepted — the upper boundary of the length rule.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void Validate_NameOfExactly200Characters_PassesNameRule_Test()
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
    public void Validate_NegativePrice_FailsPriceRule_Test(decimal price)
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
    public void Validate_ValidCommand_ProducesNoErrors_Test(decimal price)
    {
        // Arrange
        var command = new CreateProductCommand(ValidName, price);

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
