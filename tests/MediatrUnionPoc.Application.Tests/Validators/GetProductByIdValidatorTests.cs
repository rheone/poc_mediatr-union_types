using FluentValidation.TestHelper;
using MediatrUnionPoc.Application.Features.Products.GetById;

namespace MediatrUnionPoc.Application.Tests.Validators;

/// <summary>Exercises <see cref="GetProductByIdValidator"/>'s validation rules directly.</summary>
public class GetProductByIdValidatorTests
{
    // SWEEP-AMBIGUITY: validating a null command throws InvalidOperationException ("Cannot pass null model to
    // Validate") from FluentValidation rather than ArgumentNullException / a null command should throw
    // ArgumentNullException naming "instance", but no such test is written because production does not do that.
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
}
