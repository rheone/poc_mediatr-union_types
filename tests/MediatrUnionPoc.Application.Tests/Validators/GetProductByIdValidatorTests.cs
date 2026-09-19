using FluentValidation.TestHelper;
using MediatrUnionPoc.Application.Features.Products.GetById;

namespace MediatrUnionPoc.Application.Tests.Validators;

/// <summary>Exercises <see cref="GetProductByIdValidator"/>'s validation rules directly.</summary>
public class GetProductByIdValidatorTests
{
    private static readonly Guid SomeId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly GetProductByIdValidator _sut = new();

    /// <summary>Verifies an empty id fails validation.</summary>
    [Fact]
    public void Validate_EmptyId_FailsIdRule_Test()
    {
        // Arrange
        var query = new GetProductByIdQuery(Guid.Empty);

        // Act
        var result = _sut.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    /// <summary>Verifies a non-empty id produces no validation errors.</summary>
    [Fact]
    public void Validate_NonEmptyId_ProducesNoErrors_Test()
    {
        // Arrange
        var query = new GetProductByIdQuery(SomeId);

        // Act
        var result = _sut.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>Verifies validating a null <see cref="GetProductByIdQuery"/> throws FluentValidation's own <see cref="InvalidOperationException"/> ("Cannot pass null model to Validate"); this validator deliberately does not override that with an <see cref="ArgumentNullException"/>.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void Validate_NullModel_ThrowsInvalidOperationException_Test()
    {
        // Act
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _sut.Validate((GetProductByIdQuery)null!)
        );

        // Assert
        Assert.Contains("null model", ex.Message);
    }
}
