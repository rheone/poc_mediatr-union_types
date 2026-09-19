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
    /// <summary>
    /// Verifies <see cref="ValidationErrors.ToErrorMessage"/> joins multiple error messages with
    /// "; ".
    /// </summary>
    [Fact]
    public void ToErrorMessage_multiple_errors_joins_messages_with_a_semicolon()
    {
        // Arrange
        var errors = new ValidationErrors([
            new ValidationError("Name", "must not be empty"),
            new ValidationError("Price", "must be greater than or equal to 0"),
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
    public void ToErrorMessage_single_error_returns_the_message_unchanged()
    {
        // Arrange
        var errors = new ValidationErrors([new ValidationError("Id", "must not be empty")]);

        // Act
        var message = errors.ToErrorMessage();

        // Assert
        Assert.Equal("must not be empty", message);
    }

    // Auto Generated, verify expected behavior:
    /// <summary>Verifies <see cref="ValidationErrors.ToErrorMessage"/> returns an empty string when there are no errors.</summary>
    [Fact]
    public void ToErrorMessage_no_errors_returns_an_empty_string()
    {
        // Arrange
        var errors = new ValidationErrors([]);

        // Act
        var message = errors.ToErrorMessage();

        // Assert
        Assert.Equal(string.Empty, message);
    }

    // Auto Generated, verify expected behavior:
    /// <summary>Verifies <see cref="ValidationErrors.ToErrorMessage"/> drops property names, keeping only messages.</summary>
    [Fact]
    public void ToErrorMessage_omits_property_names()
    {
        // Arrange
        var errors = new ValidationErrors([new ValidationError("Name", "must not be empty")]);

        // Act
        var message = errors.ToErrorMessage();

        // Assert
        Assert.DoesNotContain("Name", message);
    }
}
