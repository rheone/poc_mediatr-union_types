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
    private const string InfrastructureFailureMessage = "infra failure";

    /// <summary>Verifies <see cref="Error.Cause"/> defaults to <see langword="null"/> when not supplied.</summary>
    [Fact]
    public void Cause_NotSupplied_DefaultsToNull_Test()
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
    public void Cause_Supplied_CarriesException_Test()
    {
        // Arrange
        var exception = new InvalidOperationException(InfrastructureFailureMessage);

        // Act
        var error = new Error(Message, Code, exception);

        // Assert
        Assert.Same(exception, error.Cause);
    }

    /// <summary>Verifies <see cref="Error.Cause"/> never appears in serialized JSON, even when set.</summary>
    [Fact]
    public void Serialize_ErrorWithCause_ExcludesCauseFromJson_Test()
    {
        // Arrange
        var error = new Error(
            Message,
            Code,
            new InvalidOperationException(InfrastructureFailureMessage)
        );

        // Act
        var json = JsonSerializer.Serialize(error);

        // Assert
        Assert.DoesNotContain("Cause", json);
    }

    /// <summary>Verifies the message and code still serialize when <see cref="Error.Cause"/> is excluded, so the exclusion isn't dropping the whole payload.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void Serialize_ErrorWithCause_IncludesMessageAndCode_Test()
    {
        // Arrange
        var error = new Error(
            Message,
            Code,
            new InvalidOperationException(InfrastructureFailureMessage)
        );

        // Act
        var json = JsonSerializer.Serialize(error);

        // Assert
        using var document = JsonDocument.Parse(json);
        Assert.Multiple(
            () => Assert.Equal(Message, document.RootElement.GetProperty("Message").GetString()),
            () => Assert.Equal(Code, document.RootElement.GetProperty("Code").GetString())
        );
    }

    /// <summary>Verifies the positional constructor rejects a null message.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void Ctor_NullMessage_ThrowsArgumentNullException_Test()
    {
        // Act
        var ex = Assert.Throws<ArgumentNullException>(() => new Error(null!));

        // Assert
        Assert.Equal("Message", ex.ParamName);
    }
}
