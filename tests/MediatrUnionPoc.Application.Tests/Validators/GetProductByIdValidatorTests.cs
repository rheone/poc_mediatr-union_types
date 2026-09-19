using FluentValidation.TestHelper;
using MediatrUnionPoc.Application.Features.Products.GetById;

namespace MediatrUnionPoc.Application.Tests.Validators;

/// <summary>Exercises <see cref="GetProductByIdValidator"/>'s validation rules directly.</summary>
public class GetProductByIdValidatorTests
{
    private readonly GetProductByIdValidator _sut = new();

    /// <summary>Verifies an empty id fails validation.</summary>
    [Fact]
    public void Empty_id_fails_validation()
    {
        var result = _sut.TestValidate(new GetProductByIdQuery(Guid.Empty));

        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    /// <summary>Verifies a non-empty id produces no validation errors.</summary>
    [Fact]
    public void Non_empty_id_produces_no_errors()
    {
        var result = _sut.TestValidate(new GetProductByIdQuery(Guid.NewGuid()));

        result.ShouldNotHaveAnyValidationErrors();
    }
}
