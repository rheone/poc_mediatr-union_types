using FluentValidation.TestHelper;
using MediatrUnionPoc.Application.Features.Products.GetPaged;

namespace MediatrUnionPoc.Application.Tests.Validators;

/// <summary>Exercises <see cref="GetPagedProductsValidator"/>'s validation rules directly.</summary>
public class GetPagedProductsValidatorTests
{
    // SWEEP-AMBIGUITY: validating a null command throws InvalidOperationException ("Cannot pass null model to
    // Validate") from FluentValidation rather than ArgumentNullException / a null command should throw
    // ArgumentNullException naming "instance", but no such test is written because production does not do that.
    private const int ValidPageNumber = 1;
    private const int ValidPageSize = 10;

    private readonly GetPagedProductsValidator _sut = new();

    /// <summary>Verifies a page number below 1 fails validation.</summary>
    /// <param name="pageNumber">A page number value that should fail validation.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_PageNumberBelow1_FailsPageNumberRule_Test(int pageNumber)
    {
        // Arrange
        var query = new GetPagedProductsQuery(pageNumber, ValidPageSize);

        // Act
        var result = _sut.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PageNumber);
    }

    /// <summary>Verifies a page size outside the 1-100 range fails validation.</summary>
    /// <param name="pageSize">A page size value that should fail validation.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void Validate_PageSizeOutside1To100Range_FailsPageSizeRule_Test(int pageSize)
    {
        // Arrange
        var query = new GetPagedProductsQuery(ValidPageNumber, pageSize);

        // Act
        var result = _sut.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    /// <summary>Verifies a fully valid paging query produces no validation errors.</summary>
    /// <param name="pageNumber">A valid page number value.</param>
    /// <param name="pageSize">A valid page size value.</param>
    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 100)]
    [InlineData(5, 50)]
    public void Validate_ValidPagingParameters_ProducesNoErrors_Test(int pageNumber, int pageSize)
    {
        // Arrange
        var query = new GetPagedProductsQuery(pageNumber, pageSize);

        // Act
        var result = _sut.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
