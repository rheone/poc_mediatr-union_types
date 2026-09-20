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

    /// <summary>Verifies a name filter longer than the 200-character name limit fails on the name filter.</summary>
    [Fact]
    public void Validate_NameContainsOver200Characters_FailsNameContainsRule_Test()
    {
        // Arrange
        var query = new GetPagedProductsQuery(
            ValidPageNumber,
            ValidPageSize,
            NameContains: new string('a', 201)
        );

        // Act
        var result = _sut.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.NameContains);
    }

    /// <summary>Verifies an owner filter longer than the 200-character owner limit fails on the owner filter.</summary>
    [Fact]
    public void Validate_OwnerIdOver200Characters_FailsOwnerIdRule_Test()
    {
        // Arrange
        var query = new GetPagedProductsQuery(
            ValidPageNumber,
            ValidPageSize,
            OwnerId: new string('a', 201)
        );

        // Act
        var result = _sut.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.OwnerId);
    }

    /// <summary>Verifies a negative price bound fails on that bound and on no other property.</summary>
    /// <param name="min">The minimum price, if any.</param>
    /// <param name="max">The maximum price, if any.</param>
    /// <param name="failingProperty">The property expected to carry the error.</param>
    [Theory]
    [InlineData(-0.01, null, nameof(GetPagedProductsQuery.MinPrice))]
    [InlineData(null, -1.0, nameof(GetPagedProductsQuery.MaxPrice))]
    public void Validate_NegativePriceBound_FailsThatBound_Test(
        double? min,
        double? max,
        string failingProperty
    )
    {
        // Arrange
        var query = new GetPagedProductsQuery(
            ValidPageNumber,
            ValidPageSize,
            MinPrice: (decimal?)min,
            MaxPrice: (decimal?)max
        );

        // Act
        var result = _sut.TestValidate(query);

        // Assert
        Assert.Equal([failingProperty], result.Errors.Select(e => e.PropertyName));
    }

    /// <summary>Verifies a minimum above the maximum fails on the maximum, with a message that says why.</summary>
    [Fact]
    public void Validate_MinPriceAboveMaxPrice_FailsMaxPriceRule_Test()
    {
        // Arrange
        var query = new GetPagedProductsQuery(
            ValidPageNumber,
            ValidPageSize,
            MinPrice: 20m,
            MaxPrice: 10m
        );

        // Act
        var result = _sut.TestValidate(query);

        // Assert
        result
            .ShouldHaveValidationErrorFor(x => x.MaxPrice)
            .WithErrorMessage("MaxPrice must not be less than MinPrice.");
    }

    /// <summary>Verifies equal price bounds are a valid (exact-price) filter.</summary>
    [Fact]
    public void Validate_MinPriceEqualToMaxPrice_ProducesNoErrors_Test()
    {
        // Arrange
        var query = new GetPagedProductsQuery(
            ValidPageNumber,
            ValidPageSize,
            MinPrice: 10m,
            MaxPrice: 10m
        );

        // Act
        var result = _sut.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>Verifies an unparseable sort fails on the sort property, once per problem, carrying the parser's message.</summary>
    [Fact]
    public void Validate_InvalidSort_FailsSortRuleOncePerProblem_Test()
    {
        // Arrange
        var query = new GetPagedProductsQuery(
            ValidPageNumber,
            ValidPageSize,
            Sort: "weight,name,-name"
        );

        // Act
        var result = _sut.TestValidate(query);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(["Sort", "Sort"], result.Errors.Select(e => e.PropertyName)),
            () =>
                Assert.Contains("'weight' is not a sortable field", result.Errors[0].ErrorMessage),
            () => Assert.Contains("'name' is listed more than once", result.Errors[1].ErrorMessage)
        );
    }

    /// <summary>Verifies a well-formed sort and every filter together produce no errors.</summary>
    [Fact]
    public void Validate_AllFiltersAndValidSort_ProducesNoErrors_Test()
    {
        // Arrange
        var query = new GetPagedProductsQuery(
            2,
            25,
            "widget",
            1m,
            99.99m,
            "owner-1",
            "name,-price,createdAt"
        );

        // Act
        var result = _sut.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>Verifies validating a null <see cref="GetPagedProductsQuery"/> throws FluentValidation's own <see cref="InvalidOperationException"/> ("Cannot pass null model to Validate"); this validator deliberately does not override that with an <see cref="ArgumentNullException"/>.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void Validate_NullModel_ThrowsInvalidOperationException_Test()
    {
        // Act
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _sut.Validate((GetPagedProductsQuery)null!)
        );

        // Assert
        Assert.Contains("null model", ex.Message);
    }
}
