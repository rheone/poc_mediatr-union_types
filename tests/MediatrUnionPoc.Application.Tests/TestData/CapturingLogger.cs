using Microsoft.Extensions.Logging;

namespace MediatrUnionPoc.Application.Tests.TestData;

/// <summary>One log call captured by <see cref="CapturingLogger{T}"/>.</summary>
/// <param name="Level">The severity the call was made at.</param>
/// <param name="EventId">The event id the call carried (default when none was given).</param>
/// <param name="Message">The fully rendered message.</param>
/// <param name="Template">The message template, i.e. the <c>{OriginalFormat}</c> value before placeholders were substituted.</param>
/// <param name="Exception">The exception attached to the call, if any.</param>
/// <param name="Properties">The structured placeholder values by name, excluding <c>{OriginalFormat}</c>.</param>
public sealed record CapturedLogEntry(
    LogLevel Level,
    EventId EventId,
    string Message,
    string? Template,
    Exception? Exception,
    IReadOnlyDictionary<string, object?> Properties
);

/// <summary>
/// A test-local <see cref="ILogger{TCategoryName}"/> that records every call so tests can assert
/// what was logged: level, template, structured values and attached exception.
/// </summary>
/// <typeparam name="T">The logger's category type.</typeparam>
public sealed class CapturingLogger<T> : ILogger<T>
{
    private const string OriginalFormatKey = "{OriginalFormat}";

    private readonly List<CapturedLogEntry> _entries = [];

    /// <summary>Gets every call captured so far, in call order.</summary>
    public IReadOnlyList<CapturedLogEntry> Entries => _entries;

    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc/>
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        ArgumentNullException.ThrowIfNull(formatter);

        var pairs = state as IEnumerable<KeyValuePair<string, object?>> ?? [];
        var properties = new Dictionary<string, object?>();
        string? template = null;
        foreach (var (key, value) in pairs)
        {
            if (key == OriginalFormatKey)
            {
                template = value as string;
            }
            else
            {
                properties[key] = value;
            }
        }

        _entries.Add(
            new CapturedLogEntry(
                logLevel,
                eventId,
                formatter(state, exception),
                template,
                exception,
                properties
            )
        );
    }
}
