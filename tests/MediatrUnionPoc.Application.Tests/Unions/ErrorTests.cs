using System.Text.Json;
using MediatrUnionPoc.Application.Common.Results;

namespace MediatrUnionPoc.Application.Tests.Unions;

/// <summary>
/// Verifies <see cref="Error"/>'s optional <see cref="Error.Cause"/> — a diagnostics-only slot for
/// the exception (if any) that produced this error, never intended to reach an API consumer.
/// </summary>
public class ErrorTests
{
    /// <summary>Verifies <see cref="Error.Cause"/> defaults to <see langword="null"/> when not supplied.</summary>
    [Fact]
    public void Cause_defaults_to_null_when_not_supplied()
    {
        var error = new Error("boom", "BOOM");

        Assert.Null(error.Cause);
    }

    /// <summary>Verifies <see cref="Error.Cause"/> carries the exception it's constructed with.</summary>
    [Fact]
    public void Cause_carries_the_supplied_exception()
    {
        var exception = new InvalidOperationException("infra failure");

        var error = new Error("boom", "BOOM", exception);

        Assert.Same(exception, error.Cause);
    }

    /// <summary>Verifies <see cref="Error.Cause"/> never appears in serialized JSON, even when set.</summary>
    [Fact]
    public void Cause_is_excluded_from_serialized_json()
    {
        var error = new Error("boom", "BOOM", new InvalidOperationException("infra failure"));

        var json = JsonSerializer.Serialize(error);

        Assert.DoesNotContain("Cause", json);
    }
}
