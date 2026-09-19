using System.Text.Json;
using MediatrUnionPoc.Application.Common.Results;

namespace MediatrUnionPoc.Application.Tests.Unions;

/// <summary>
/// Verifies <see cref="Error"/>'s optional <see cref="Error.Cause"/> — a diagnostics-only slot for
/// the exception (if any) that produced this error, never intended to reach an API consumer.
/// </summary>
public class ErrorTests
{
    private const string Message = "boom";
    private const string Code = "BOOM";

    /// <summary>Verifies <see cref="Error.Cause"/> defaults to <see langword="null"/> when not supplied.</summary>
    [Fact]
    public void Cause_not_supplied_defaults_to_null()
    {
        // Arrange
        var error = new Error(Message, Code);

        // Act
        var cause = error.Cause;

        // Assert
        Assert.Null(cause);
    }

    /// <summary>Verifies <see cref="Error.Cause"/> carries the exception it's constructed with.</summary>
    [Fact]
    public void Cause_supplied_carries_the_exception()
    {
        // Arrange
        var exception = new InvalidOperationException("infra failure");

        // Act
        var error = new Error(Message, Code, exception);

        // Assert
        Assert.Same(exception, error.Cause);
    }

    /// <summary>Verifies <see cref="Error.Cause"/> never appears in serialized JSON, even when set.</summary>
    [Fact]
    public void Serialize_with_a_cause_excludes_it_from_the_json()
    {
        // Arrange
        var error = new Error(Message, Code, new InvalidOperationException("infra failure"));

        // Act
        var json = JsonSerializer.Serialize(error);

        // Assert
        Assert.DoesNotContain("Cause", json);
    }

    // Auto Generated, verify expected behavior:
    /// <summary>Verifies the message and code still serialize when <see cref="Error.Cause"/> is excluded, so the exclusion isn't dropping the whole payload.</summary>
    [Fact]
    public void Serialize_includes_the_message_and_code()
    {
        // Arrange
        var error = new Error(Message, Code, new InvalidOperationException("infra failure"));

        // Act
        var json = JsonSerializer.Serialize(error);

        // Assert
        using var document = JsonDocument.Parse(json);
        Assert.Equal(Message, document.RootElement.GetProperty("Message").GetString());
        Assert.Equal(Code, document.RootElement.GetProperty("Code").GetString());
    }
}
