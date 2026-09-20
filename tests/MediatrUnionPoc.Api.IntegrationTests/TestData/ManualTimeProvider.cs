namespace MediatrUnionPoc.Api.IntegrationTests.TestData;

/// <summary>A <see cref="TimeProvider"/> whose current time only changes when a test says so, so creation timestamps over HTTP are deterministic.</summary>
/// <param name="now">The instant the clock starts at.</param>
public sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;

    /// <summary>Moves the clock to <paramref name="instant"/>.</summary>
    /// <param name="instant">The new current time.</param>
    public void SetUtcNow(DateTimeOffset instant) => _now = instant;

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => _now;
}
