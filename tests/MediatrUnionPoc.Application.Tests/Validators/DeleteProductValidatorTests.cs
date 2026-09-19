using FluentValidation.TestHelper;
using MediatrUnionPoc.Application.Features.Products.Delete;
using MediatrUnionPoc.Application.Tests.TestData;

namespace MediatrUnionPoc.Application.Tests.Validators;

/// <summary>Exercises <see cref="DeleteProductValidator"/>, whose one rule feeds <see cref="DeleteProductResult.FromValidationErrors"/> rather than a <c>ValidationErrors</c> case of its own.</summary>
public class DeleteProductValidatorTests
{
    private static readonly Guid SomeId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly DeleteProductValidator _sut = new();

    /// <summary>Verifies an empty id fails validation.</summary>
    [Fact]
    public void Validate_empty_id_fails()
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
    public void Validate_non_empty_id_produces_no_errors()
    {
        // Arrange
        var command = new DeleteProductCommand(SomeId, PrincipalMother.Anonymous());

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
