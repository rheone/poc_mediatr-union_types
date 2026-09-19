using FluentValidation.TestHelper;
using MediatrUnionPoc.Application.Features.Products.Update;
using MediatrUnionPoc.Application.Tests.TestData;

namespace MediatrUnionPoc.Application.Tests.Validators;

/// <summary>Exercises <see cref="UpdateProductValidator"/>'s rules directly, including the id check that <see cref="MediatrUnionPoc.Application.Features.Products.Create.CreateProductValidator"/> doesn't need.</summary>
public class UpdateProductValidatorTests
{
    private const string ValidName = "Widget";
    private const decimal ValidPrice = 10m;
    private const int MaxNameLength = 200;

    private static readonly Guid SomeId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private readonly UpdateProductValidator _sut = new();

    /// <summary>Verifies an empty id fails validation.</summary>
    [Fact]
    public void Validate_empty_id_fails()
    {
        // Arrange
        var command = Command(id: Guid.Empty);

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

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
        var command = Command(name: name!);

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    // Auto Generated, verify expected behavior:
    /// <summary>Verifies a name over 200 characters fails validation.</summary>
    [Fact]
    public void Validate_name_over_200_characters_fails()
    {
        // Arrange
        var command = Command(name: new string('a', MaxNameLength + 1));

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
        var command = Command(name: new string('a', MaxNameLength));

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
        var command = Command(price: price);

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Price);
    }

    // Auto Generated, verify expected behavior:
    /// <summary>Verifies a zero price is accepted — the lower boundary of the price rule.</summary>
    [Fact]
    public void Validate_zero_price_passes()
    {
        // Arrange
        var command = Command(price: 0m);

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Price);
    }

    /// <summary>Verifies a fully valid command produces no validation errors.</summary>
    [Fact]
    public void Validate_valid_command_produces_no_errors()
    {
        // Arrange
        var command = Command();

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    private static UpdateProductCommand Command(
        Guid? id = null,
        string name = ValidName,
        decimal price = ValidPrice
    ) => new(id ?? SomeId, name, price, PrincipalMother.Anonymous());
}
