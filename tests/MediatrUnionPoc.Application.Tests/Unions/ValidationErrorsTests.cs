using MediatrUnionPoc.Application.Common.Results;

namespace MediatrUnionPoc.Application.Tests.Unions;

/// <summary>
/// <see cref="ValidationErrors.ToErrorMessage"/> is the one place the "join messages with '; '"
/// format decision lives — every union without its own <c>ValidationErrors</c> case
/// (<c>DeleteProductResult</c>, <c>GetProductByIdResult</c>, <c>GetPagedProductsResult</c>) calls
/// it from <c>FromValidationErrors</c> instead of formatting the message itself.
/// </summary>
public class ValidationErrorsTests
{
    private const string MustNotBeEmpty = "must not be empty";
    private const string MustBeNonNegative = "must be greater than or equal to 0";

    /// <summary>
    /// Verifies <see cref="ValidationErrors.ToErrorMessage"/> joins multiple error messages with
    /// "; ".
    /// </summary>
    [Fact]
    public void ToErrorMessage_MultipleErrors_JoinsMessagesWithSemicolon_Test()
    {
        // Arrange
        var errors = new ValidationErrors([
            new ValidationError("Name", MustNotBeEmpty),
            new ValidationError("Price", MustBeNonNegative),
        ]);

        // Act
        var message = errors.ToErrorMessage();

        // Assert
        Assert.Equal("must not be empty; must be greater than or equal to 0", message);
    }

    /// <summary>
    /// Verifies <see cref="ValidationErrors.ToErrorMessage"/> returns a single error message
    /// as-is, with no separator applied.
    /// </summary>
    [Fact]
    public void ToErrorMessage_SingleError_ReturnsMessageUnchanged_Test()
    {
        // Arrange
        var errors = new ValidationErrors([new ValidationError("Id", MustNotBeEmpty)]);

        // Act
        var message = errors.ToErrorMessage();

        // Assert
        Assert.Equal(MustNotBeEmpty, message);
    }

    /// <summary>Verifies <see cref="ValidationErrors.ToErrorMessage"/> returns an empty string when there are no errors.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void ToErrorMessage_NoErrors_ReturnsEmptyString_Test()
    {
        // Arrange
        var errors = new ValidationErrors([]);

        // Act
        var message = errors.ToErrorMessage();

        // Assert
        Assert.Equal(string.Empty, message);
    }

    /// <summary>Verifies <see cref="ValidationErrors.ToErrorMessage"/> drops property names, keeping only messages.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void ToErrorMessage_ErrorWithPropertyName_OmitsPropertyName_Test()
    {
        // Arrange
        var errors = new ValidationErrors([new ValidationError("Name", MustNotBeEmpty)]);

        // Act
        var message = errors.ToErrorMessage();

        // Assert
        Assert.DoesNotContain("Name", message);
    }
}
