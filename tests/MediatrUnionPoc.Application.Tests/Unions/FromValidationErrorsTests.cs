using System.Runtime.CompilerServices;
using MediatrUnionPoc.Application.Common.Abstractions;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Features.Products.Create;
using MediatrUnionPoc.Application.Features.Products.Delete;
using MediatrUnionPoc.Application.Features.Products.GetById;
using MediatrUnionPoc.Application.Features.Products.GetPaged;
using MediatrUnionPoc.Application.Features.Products.Update;

namespace MediatrUnionPoc.Application.Tests.Unions;

/// <summary>
/// Verifies how each result union builds its <see cref="IValidatable{TSelf}.FromValidationErrors"/>
/// case: unions that declare a <see cref="ValidationErrors"/> case keep it as-is, unions that
/// don't fold it into an <see cref="Error"/> carrying <see cref="Error.ValidationFailureCode"/>.
/// <c>ValidationBehavior</c> depends on either shape being produced generically.
/// </summary>
public class FromValidationErrorsTests
{
    private const string PropertyName = "Name";
    private const string ErrorMessage = "must not be empty";

    private static readonly ValidationErrors SomeErrors = new([
        new ValidationError(PropertyName, ErrorMessage),
    ]);

    /// <summary>Verifies unions that declare a <see cref="ValidationErrors"/> case carry the given errors through unchanged.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void FromValidationErrors_UnionWithValidationErrorsCase_CarriesErrorsUnchanged_Test()
    {
        // Arrange
        // (SomeErrors)

        // Act
        var create = CreateProductResult.FromValidationErrors(SomeErrors);
        var update = UpdateProductResult.FromValidationErrors(SomeErrors);

        // Assert
        Assert.Multiple(
            () => Assert.Same(SomeErrors, ((IUnion)create).Value),
            () => Assert.Same(SomeErrors, ((IUnion)update).Value)
        );
    }

    /// <summary>Verifies unions without their own <see cref="ValidationErrors"/> case fold it into an <see cref="Error"/> coded <see cref="Error.ValidationFailureCode"/> whose message includes the validation messages.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void FromValidationErrors_UnionWithoutValidationErrorsCase_FoldsIntoCodedError_Test()
    {
        // Arrange
        // (SomeErrors)

        // Act
        var results = new IUnion[]
        {
            DeleteProductResult.FromValidationErrors(SomeErrors),
            GetProductByIdResult.FromValidationErrors(SomeErrors),
            GetPagedProductsResult.FromValidationErrors(SomeErrors),
        };

        // Assert
        Assert.All(
            results,
            result =>
            {
                var error = Assert.IsType<Error>(result.Value);
                Assert.Equal(Error.ValidationFailureCode, error.Code);
                Assert.Contains(SomeErrors.ToErrorMessage(), error.Message);
            }
        );
    }

    /// <summary>Verifies <see cref="CreateProductResult.FromValidationErrors"/> rejects a null argument instead of wrapping it or failing later.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void FromValidationErrors_CreateProductNullErrors_ThrowsArgumentNullException_Test()
    {
        // Act
        var ex = Assert.Throws<ArgumentNullException>(() =>
            CreateProductResult.FromValidationErrors(null!)
        );

        // Assert
        Assert.Equal("errors", ex.ParamName);
    }

    /// <summary>Verifies <see cref="UpdateProductResult.FromValidationErrors"/> rejects a null argument instead of wrapping it or failing later.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void FromValidationErrors_UpdateProductNullErrors_ThrowsArgumentNullException_Test()
    {
        // Act
        var ex = Assert.Throws<ArgumentNullException>(() =>
            UpdateProductResult.FromValidationErrors(null!)
        );

        // Assert
        Assert.Equal("errors", ex.ParamName);
    }

    /// <summary>Verifies <see cref="DeleteProductResult.FromValidationErrors"/> rejects a null argument instead of wrapping it or failing later.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void FromValidationErrors_DeleteProductNullErrors_ThrowsArgumentNullException_Test()
    {
        // Act
        var ex = Assert.Throws<ArgumentNullException>(() =>
            DeleteProductResult.FromValidationErrors(null!)
        );

        // Assert
        Assert.Equal("errors", ex.ParamName);
    }

    /// <summary>Verifies <see cref="GetProductByIdResult.FromValidationErrors"/> rejects a null argument instead of wrapping it or failing later.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void FromValidationErrors_GetProductByIdNullErrors_ThrowsArgumentNullException_Test()
    {
        // Act
        var ex = Assert.Throws<ArgumentNullException>(() =>
            GetProductByIdResult.FromValidationErrors(null!)
        );

        // Assert
        Assert.Equal("errors", ex.ParamName);
    }

    /// <summary>Verifies <see cref="GetPagedProductsResult.FromValidationErrors"/> rejects a null argument instead of wrapping it or failing later.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void FromValidationErrors_GetPagedProductsNullErrors_ThrowsArgumentNullException_Test()
    {
        // Act
        var ex = Assert.Throws<ArgumentNullException>(() =>
            GetPagedProductsResult.FromValidationErrors(null!)
        );

        // Assert
        Assert.Equal("errors", ex.ParamName);
    }
}
