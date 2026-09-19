using FluentValidation.TestHelper;
using MediatrUnionPoc.Application.Features.Products.GetPaged;

namespace MediatrUnionPoc.Application.Tests.Validators;

/// <summary>Exercises <see cref="GetPagedProductsValidator"/>'s validation rules directly.</summary>
public class GetPagedProductsValidatorTests
{
    private const int ValidPageNumber = 1;
    private const int ValidPageSize = 10;

    private readonly GetPagedProductsValidator _sut = new();

    /// <summary>Verifies a page number below 1 fails validation.</summary>
    /// <param name="pageNumber">A page number value that should fail validation.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_page_number_below_1_fails(int pageNumber)
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
    public void Validate_page_size_outside_1_to_100_range_fails(int pageSize)
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
    public void Validate_valid_paging_parameters_produce_no_errors(int pageNumber, int pageSize)
    {
        // Arrange
        var query = new GetPagedProductsQuery(pageNumber, pageSize);

        // Act
        var result = _sut.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
