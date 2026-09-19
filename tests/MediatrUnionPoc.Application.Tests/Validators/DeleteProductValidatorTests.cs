using System.Security.Claims;
using FluentValidation.TestHelper;
using MediatrUnionPoc.Application.Features.Products.Delete;

namespace MediatrUnionPoc.Application.Tests.Validators;

/// <summary>Exercises <see cref="DeleteProductValidator"/>, whose one rule feeds <see cref="DeleteProductResult.FromValidationErrors"/> rather than a <c>ValidationErrors</c> case of its own.</summary>
public class DeleteProductValidatorTests
{
    private readonly DeleteProductValidator _sut = new();

    /// <summary>Verifies an empty id fails validation.</summary>
    [Fact]
    public void Empty_id_fails_validation()
    {
        var result = _sut.TestValidate(new DeleteProductCommand(Guid.Empty, new ClaimsPrincipal()));

        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    /// <summary>Verifies a non-empty id produces no validation errors.</summary>
    [Fact]
    public void Non_empty_id_produces_no_errors()
    {
        var result = _sut.TestValidate(
            new DeleteProductCommand(Guid.NewGuid(), new ClaimsPrincipal())
        );

        result.ShouldNotHaveAnyValidationErrors();
    }
}
