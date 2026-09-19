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
// Auto Generated, verify expected behavior:
public class FromValidationErrorsTests
{
    private static readonly ValidationErrors SomeErrors = new([
        new ValidationError("Name", "must not be empty"),
    ]);

    /// <summary>Verifies unions that declare a <see cref="ValidationErrors"/> case carry the given errors through unchanged.</summary>
    [Fact]
    public void FromValidationErrors_union_with_a_ValidationErrors_case_carries_the_errors_unchanged()
    {
        // Arrange
        // (SomeErrors)

        // Act
        var create = CreateProductResult.FromValidationErrors(SomeErrors);
        var update = UpdateProductResult.FromValidationErrors(SomeErrors);

        // Assert
        Assert.Same(SomeErrors, ((IUnion)create).Value);
        Assert.Same(SomeErrors, ((IUnion)update).Value);
    }

    /// <summary>Verifies unions without their own <see cref="ValidationErrors"/> case fold it into an <see cref="Error"/> coded <see cref="Error.ValidationFailureCode"/> whose message includes the validation messages.</summary>
    [Fact]
    public void FromValidationErrors_union_without_a_ValidationErrors_case_folds_into_a_coded_Error()
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
}
