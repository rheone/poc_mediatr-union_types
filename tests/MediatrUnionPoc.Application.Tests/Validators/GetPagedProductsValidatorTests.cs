using FluentValidation.TestHelper;
using MediatrUnionPoc.Application.Features.Products.GetPaged;

namespace MediatrUnionPoc.Application.Tests.Validators;

/// <summary>Exercises <see cref="GetPagedProductsValidator"/>'s validation rules directly.</summary>
public class GetPagedProductsValidatorTests
{
    private readonly GetPagedProductsValidator _sut = new();

    /// <summary>Verifies a page number below 1 fails validation.</summary>
    /// <param name="pageNumber">A page number value that should fail validation.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void PageNumber_below_1_fails_validation(int pageNumber)
    {
        var result = _sut.TestValidate(new GetPagedProductsQuery(pageNumber, 10));

        result.ShouldHaveValidationErrorFor(x => x.PageNumber);
    }

    /// <summary>Verifies a page size outside the 1-100 range fails validation.</summary>
    /// <param name="pageSize">A page size value that should fail validation.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void PageSize_outside_1_to_100_range_fails_validation(int pageSize)
    {
        var result = _sut.TestValidate(new GetPagedProductsQuery(1, pageSize));

        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    /// <summary>Verifies a fully valid paging query produces no validation errors.</summary>
    /// <param name="pageNumber">A valid page number value.</param>
    /// <param name="pageSize">A valid page size value.</param>
    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 100)]
    [InlineData(5, 50)]
    public void Valid_paging_parameters_produce_no_errors(int pageNumber, int pageSize)
    {
        var result = _sut.TestValidate(new GetPagedProductsQuery(pageNumber, pageSize));

        result.ShouldNotHaveAnyValidationErrors();
    }
}
