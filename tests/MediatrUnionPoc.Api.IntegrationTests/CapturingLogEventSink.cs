using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>
/// A Serilog sink that records every event the host's logger emits, registered in DI by
/// <see cref="ProductsApiFactory"/> so <c>ReadFrom.Services</c> picks it up. It sees what the
/// configured sinks see (after the configured minimum levels and overrides), together with every
/// enriched property.
/// </summary>
internal sealed class CapturingLogEventSink : ILogEventSink
{
    private readonly ConcurrentQueue<LogEvent> _events = new();

    /// <summary>Gets a snapshot of everything logged so far.</summary>
    public IReadOnlyList<LogEvent> Events => [.. _events];

    /// <inheritdoc/>
    public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);
}

/// <summary>Readers for the properties of a captured <see cref="LogEvent"/>.</summary>
internal static class LogEventExtensions
{
    /// <summary>Gets the scalar value of the named property, or <see langword="null"/> when it is absent or not a scalar.</summary>
    /// <param name="logEvent">The captured event.</param>
    /// <param name="name">The property name.</param>
    /// <returns>The scalar value, or <see langword="null"/>.</returns>
    public static object? Scalar(this LogEvent logEvent, string name) =>
        logEvent.Properties.TryGetValue(name, out var value) && value is ScalarValue scalar
            ? scalar.Value
            : null;

    /// <summary>Gets the boolean value of the named scalar property, or <see langword="null"/> when it is absent or not a boolean.</summary>
    /// <param name="logEvent">The captured event.</param>
    /// <param name="name">The property name.</param>
    /// <returns>The flag, or <see langword="null"/>.</returns>
    public static bool? Flag(this LogEvent logEvent, string name) => logEvent.Scalar(name) as bool?;

    /// <summary>Gets the numeric id of the event's <c>EventId</c> property, or <see langword="null"/> when it has none.</summary>
    /// <param name="logEvent">The captured event.</param>
    /// <returns>The event id number.</returns>
    public static int? EventIdNumber(this LogEvent logEvent) =>
        logEvent.Properties.TryGetValue("EventId", out var value)
        && value is StructureValue structure
        && structure.Properties.FirstOrDefault(property => property.Name == "Id")?.Value
            is ScalarValue { Value: int id }
            ? id
            : null;

    /// <summary>Gets whether the event was written by a logger whose category is or starts with <paramref name="sourceContext"/>.</summary>
    /// <param name="logEvent">The captured event.</param>
    /// <param name="sourceContext">The category (or a prefix of it).</param>
    /// <returns><see langword="true"/> when the category matches.</returns>
    public static bool From(this LogEvent logEvent, string sourceContext) =>
        logEvent.Scalar("SourceContext") is string category
        && category.StartsWith(sourceContext, StringComparison.Ordinal);

    /// <summary>Gets the message template text, for finding an event by its shape.</summary>
    /// <param name="logEvent">The captured event.</param>
    /// <returns>The template text.</returns>
    public static string Template(this LogEvent logEvent) => logEvent.MessageTemplate.Text;

    /// <summary>Renders everything a sink could write for the event (message, every property, the exception) as one string.</summary>
    /// <param name="logEvent">The captured event.</param>
    /// <returns>The rendered text.</returns>
    public static string RenderEverything(this LogEvent logEvent)
    {
        var properties = string.Join(
            "|",
            logEvent.Properties.Select(pair => pair.Key + "=" + pair.Value)
        );
        return $"{logEvent.RenderMessage()}|{properties}|{logEvent.Exception}";
    }
}
