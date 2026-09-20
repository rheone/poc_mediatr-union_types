using FluentValidation.TestHelper;
using MediatrUnionPoc.Application.Common;
using MediatrUnionPoc.Application.Features.Products.Patch;
using MediatrUnionPoc.Application.Tests.TestData;
using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Application.Tests.Validators;

/// <summary>Exercises <see cref="PatchProductValidator"/>: the patch must name something, and every field it does name obeys the same rules as create and update.</summary>
public class PatchProductValidatorTests
{
    private const string ValidName = "Widget";
    private const decimal ValidPrice = 10m;
    private const int MaxNameLength = 200;

    private static readonly Guid SomeId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    private readonly PatchProductValidator _sut = new();

    /// <summary>Verifies a patch that supplies neither field is invalid, since it would change nothing.</summary>
    [Fact]
    public void Validate_NeitherFieldSupplied_FailsWithNothingToPatchError_Test()
    {
        // Arrange
        var command = Command();

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        var error = Assert.Single(result.Errors);
        Assert.Contains("at least one", error.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies a supplied name that is null (an explicit JSON null), empty or whitespace fails the name rule — a name cannot be cleared.</summary>
    /// <param name="name">The supplied but unusable name.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("	")]
    public void Validate_SuppliedNullEmptyOrWhitespaceName_FailsNameRule_Test(string? name)
    {
        // Arrange
        var command = Command(name: Optional<string?>.Of(name));

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        Assert.Contains(result.Errors, e => e.PropertyName == "Name");
    }

    /// <summary>Verifies a supplied name over 200 characters fails and one of exactly 200 is accepted.</summary>
    /// <param name="length">The supplied name's length.</param>
    /// <param name="valid">Whether that length is acceptable.</param>
    [Theory]
    [InlineData(MaxNameLength, true)]
    [InlineData(MaxNameLength + 1, false)]
    public void Validate_SuppliedNameLength_EnforcesTheLimit_Test(int length, bool valid)
    {
        // Arrange
        var command = Command(name: Optional<string?>.Of(new string('a', length)));

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        Assert.Equal(valid, result.IsValid);
    }

    /// <summary>Verifies a supplied price that is null (an explicit JSON null) or negative fails the price rule, while zero is accepted.</summary>
    /// <param name="price">The supplied price.</param>
    /// <param name="valid">Whether that price is acceptable.</param>
    [Theory]
    [InlineData(null, false)]
    [InlineData("-0.01", false)]
    [InlineData("0", true)]
    [InlineData("24.99", true)]
    public void Validate_SuppliedPrice_EnforcesTheRule_Test(string? price, bool valid)
    {
        // Arrange
        decimal? value = price is null
            ? null
            : decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture);
        var command = Command(price: Optional<decimal?>.Of(value));

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(valid, result.IsValid),
            () => Assert.Equal(valid, result.Errors.All(e => e.PropertyName != "Price"))
        );
    }

    /// <summary>Verifies an absent field is not validated: a patch that only supplies a valid price is valid.</summary>
    [Fact]
    public void Validate_OnlyValidPriceSupplied_IsValid_Test()
    {
        // Arrange
        var command = Command(price: Optional<decimal?>.Of(ValidPrice));

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        Assert.True(result.IsValid);
    }

    /// <summary>Verifies a patch with both fields valid is valid.</summary>
    [Fact]
    public void Validate_BothFieldsValid_IsValid_Test()
    {
        // Arrange
        var command = Command(
            name: Optional<string?>.Of(ValidName),
            price: Optional<decimal?>.Of(ValidPrice)
        );

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        Assert.True(result.IsValid);
    }

    /// <summary>Verifies an empty id fails validation.</summary>
    [Fact]
    public void Validate_EmptyId_FailsIdRule_Test()
    {
        // Arrange
        var command = Command(id: Guid.Empty, name: Optional<string?>.Of(ValidName));

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    private static PatchProductCommand Command(
        Guid? id = null,
        Optional<string?> name = default,
        Optional<decimal?> price = default
    ) => new(id ?? SomeId, name, price, PrincipalMother.WithId("owner-1"), ProductVersion.Initial);
}
