using FluentValidation.TestHelper;
using MediatrUnionPoc.Application.Features.Products.Delete;
using MediatrUnionPoc.Application.Tests.TestData;

namespace MediatrUnionPoc.Application.Tests.Validators;

/// <summary>Exercises <see cref="DeleteProductValidator"/>, whose one rule feeds <see cref="DeleteProductResult.FromValidationErrors"/> rather than a <c>ValidationErrors</c> case of its own.</summary>
public class DeleteProductValidatorTests
{
    // SWEEP-AMBIGUITY: validating a null command throws InvalidOperationException ("Cannot pass null model to
    // Validate") from FluentValidation rather than ArgumentNullException / a null command should throw
    // ArgumentNullException naming "instance", but no such test is written because production does not do that.
    private static readonly Guid SomeId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly DeleteProductValidator _sut = new();

    /// <summary>Verifies an empty id fails validation.</summary>
    [Fact]
    public void Validate_EmptyId_FailsIdRule_Test()
    {
        // Arrange
        var command = new DeleteProductCommand(Guid.Empty, PrincipalMother.Anonymous());

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    /// <summary>Verifies a non-empty id produces no validation errors.</summary>
    [Fact]
    public void Validate_NonEmptyId_ProducesNoErrors_Test()
    {
        // Arrange
        var command = new DeleteProductCommand(SomeId, PrincipalMother.Anonymous());

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
