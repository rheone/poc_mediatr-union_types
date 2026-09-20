using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>One captured log call: category, level, rendered message, exception and the scopes active when it was written.</summary>
/// <param name="Category">The logger category.</param>
/// <param name="Level">The log level.</param>
/// <param name="Message">The rendered message.</param>
/// <param name="Exception">The attached exception, if any.</param>
/// <param name="Scopes">Every scope object active at the time.</param>
internal sealed record CapturedLog(
    string Category,
    LogLevel Level,
    string Message,
    Exception? Exception,
    IReadOnlyList<object?> Scopes
)
{
    /// <summary>Gets the value of the named key from any active key/value scope, or <see langword="null"/>.</summary>
    /// <param name="key">The scope key.</param>
    /// <returns>The scope value, or <see langword="null"/> when absent.</returns>
    public object? ScopeValue(string key) =>
        Scopes
            .OfType<IEnumerable<KeyValuePair<string, object?>>>()
            .SelectMany(scope => scope)
            .Where(pair => pair.Key == key)
            .Select(pair => pair.Value)
            .FirstOrDefault();
}

/// <summary>A logger provider that records every entry together with the scopes active at the time.</summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentQueue<CapturedLog> _entries = new();
    private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();

    /// <summary>Gets a snapshot of everything logged so far.</summary>
    public IReadOnlyList<CapturedLog> Entries => [.. _entries];

    /// <inheritdoc/>
    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, this);

    /// <inheritdoc/>
    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

    /// <inheritdoc/>
    public void Dispose() { }

    private sealed class CapturingLogger(string category, CapturingLoggerProvider owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => owner._scopes.Push(state);

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            var scopes = new List<object?>();
            owner._scopes.ForEachScope((scope, list) => list.Add(scope), scopes);
            owner._entries.Enqueue(
                new CapturedLog(category, logLevel, formatter(state, exception), exception, scopes)
            );
        }
    }
}
