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

    /// <summary>Verifies validating a null <see cref="DeleteProductCommand"/> throws FluentValidation's own <see cref="InvalidOperationException"/> ("Cannot pass null model to Validate"); this validator deliberately does not override that with an <see cref="ArgumentNullException"/>.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void Validate_NullModel_ThrowsInvalidOperationException_Test()
    {
        // Act
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _sut.Validate((DeleteProductCommand)null!)
        );

        // Assert
        Assert.Contains("null model", ex.Message);
    }
}
