namespace MediatrUnionPoc.Application.Tests.TestData;

/// <summary>A <see cref="TimeProvider"/> frozen at one instant, so tests can assert on timestamps a handler stamps.</summary>
/// <param name="now">The instant <see cref="GetUtcNow"/> always returns.</param>
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => now;
}
