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
    public void ToErrorMessage_joins_every_error_message_with_a_semicolon()
    {
        var errors = new ValidationErrors([
            new ValidationError("Name", "must not be empty"),
            new ValidationError("Price", "must be greater than or equal to 0"),
        ]);

        Assert.Equal(
            "must not be empty; must be greater than or equal to 0",
            errors.ToErrorMessage()
        );
    }

    /// <summary>
    /// Verifies <see cref="ValidationErrors.ToErrorMessage"/> returns a single error message
    /// as-is, with no separator applied.
    /// </summary>
    [Fact]
    public void ToErrorMessage_returns_a_single_message_unchanged()
    {
        var errors = new ValidationErrors([new ValidationError("Id", "must not be empty")]);

        Assert.Equal("must not be empty", errors.ToErrorMessage());
    }
}
