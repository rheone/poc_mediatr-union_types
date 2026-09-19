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
    public void Validate_empty_id_fails()
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
    public void Validate_non_empty_id_produces_no_errors()
    {
        // Arrange
        var query = new GetProductByIdQuery(SomeId);

        // Act
        var result = _sut.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
