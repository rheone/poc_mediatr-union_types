using System.Text;
using System.Text.Json.Nodes;
using MediatrUnionPoc.Api.Audit;
using MediatrUnionPoc.Application.Common.Auditing;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>
/// Tests <see cref="FileAuditLog"/> against a real temporary directory: JSON Lines appended per event,
/// one file per UTC day chosen by the event's timestamp, concurrent writers that never interleave,
/// free text that cannot forge a line, and an IO failure that is thrown rather than swallowed.
/// </summary>
[Trait("Category", "Integration")]
public sealed class FileAuditLogTests : IDisposable
{
    private const string Day1File = "audit-20260304.jsonl";
    private static readonly Guid AnyEventId = new("3b9d4c1a-7e52-4f08-a6d3-91c5e0b2f847");
    private static readonly DateTimeOffset Day1 = new(2026, 3, 4, 23, 59, 58, TimeSpan.Zero);

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "mediatr-union-poc-audit-tests",
        "file-log-" + Guid.NewGuid().ToString("N")
    );

    /// <summary>Removes the temporary directory.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    /// <summary>Verifies events are appended one per line, in order, to a single file, and the directory is created on the first write.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task RecordAsync_TwoEvents_AreAppendedAsTwoLinesInOneFile_Test()
    {
        // Arrange
        using var sut = new FileAuditLog(_directory);

        // Act
        await sut.RecordAsync(Event("first", Day1), CancellationToken.None);
        await sut.RecordAsync(Event("second", Day1.AddSeconds(1)), CancellationToken.None);

        // Assert
        var file = Assert.Single(Directory.GetFiles(_directory));
        var lines = await File.ReadAllLinesAsync(file, CancellationToken.None);
        Assert.Multiple(
            () => Assert.Equal(Day1File, Path.GetFileName(file)),
            () => Assert.Equal(2, lines.Length),
            () => Assert.Equal("first", JsonNode.Parse(lines[0])!["action"]!.GetValue<string>()),
            () => Assert.Equal("second", JsonNode.Parse(lines[1])!["action"]!.GetValue<string>())
        );
    }

    /// <summary>Verifies the file is UTF-8 without a byte-order mark and every line ends with a bare line feed.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task RecordAsync_Event_IsWrittenAsBomlessUtf8WithLfEndings_Test()
    {
        // Arrange
        using var sut = new FileAuditLog(_directory);

        // Act
        await sut.RecordAsync(Event("café", Day1), CancellationToken.None);

        // Assert
        var bytes = await File.ReadAllBytesAsync(
            Path.Combine(_directory, Day1File),
            CancellationToken.None
        );
        var text = new UTF8Encoding(false, true).GetString(bytes);
        Assert.Multiple(
            () => Assert.NotEqual(0xEF, bytes[0]),
            () => Assert.EndsWith("\n", text, StringComparison.Ordinal),
            () => Assert.DoesNotContain('\r', text),
            () => Assert.Single(text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        );
    }

    /// <summary>Verifies the file rolls at UTC midnight: an event stamped on the next day (by a fake clock, as the behavior stamps it) goes to the next day's file.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task RecordAsync_EventsOnTwoDays_GoToTwoFiles_Test()
    {
        // Arrange
        var clock = new ManualClock(Day1);
        using var sut = new FileAuditLog(_directory);

        // Act
        await sut.RecordAsync(Event("late", clock.GetUtcNow()), CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(5));
        await sut.RecordAsync(Event("early", clock.GetUtcNow()), CancellationToken.None);

        // Assert
        var files = Directory.GetFiles(_directory).Select(Path.GetFileName).Order().ToList();
        Assert.Equal([Day1File, "audit-20260305.jsonl"], files);
    }

    /// <summary>Verifies the day is the UTC day of the event even when its offset says otherwise.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task RecordAsync_EventWithOffset_IsFiledByItsUtcDay_Test()
    {
        // Arrange
        using var sut = new FileAuditLog(_directory);
        var lateInTokyo = new DateTimeOffset(2026, 3, 5, 1, 0, 0, TimeSpan.FromHours(9));

        // Act
        await sut.RecordAsync(Event("x", lateInTokyo), CancellationToken.None);

        // Assert
        Assert.Equal(Day1File, Path.GetFileName(Assert.Single(Directory.GetFiles(_directory))));
    }

    /// <summary>Verifies a hundred concurrent writers produce a hundred whole, parseable lines, none interleaved.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task RecordAsync_ConcurrentWriters_NeverInterleaveLines_Test()
    {
        // Arrange
        using var sut = new FileAuditLog(_directory);
        var padding = new string('x', 2_000);

        // Act
        await Task.WhenAll(
            Enumerable
                .Range(0, 100)
                .Select(i =>
                    Task.Run(
                        () =>
                            sut.RecordAsync(
                                Event($"event-{i}-{padding}", Day1),
                                CancellationToken.None
                            ),
                        CancellationToken.None
                    )
                )
        );

        // Assert
        var lines = await File.ReadAllLinesAsync(
            Path.Combine(_directory, Day1File),
            CancellationToken.None
        );
        var actions = lines.Select(line => JsonNode.Parse(line)!["action"]!.GetValue<string>());
        Assert.Equal(
            Enumerable
                .Range(0, 100)
                .Select(i => $"event-{i}-{padding}")
                .Order(StringComparer.Ordinal),
            actions.Order(StringComparer.Ordinal)
        );
    }

    /// <summary>Verifies newlines, quotes and a pasted fake record in free text end up inside one line, never as a second record.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task RecordAsync_ReasonWithNewlinesAndQuotes_StaysOneLineInTheFile_Test()
    {
        // Arrange
        using var sut = new FileAuditLog(_directory);
        const string forged = "ok\n{\"action\":\"Product.Delete\"}\r\n\"quoted\"";

        // Act
        await sut.RecordAsync(Event("real", Day1) with { Reason = forged }, CancellationToken.None);

        // Assert
        var lines = await File.ReadAllLinesAsync(
            Path.Combine(_directory, Day1File),
            CancellationToken.None
        );
        var line = Assert.Single(lines);
        var parsed = JsonNode.Parse(line)!;
        Assert.Multiple(
            () => Assert.Equal("real", parsed["action"]!.GetValue<string>()),
            () => Assert.Equal(forged, parsed["reason"]!.GetValue<string>())
        );
    }

    /// <summary>Verifies an existing file is appended to, never truncated, by a new instance (a restart).</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task RecordAsync_NewInstanceSameDirectory_AppendsToExistingFile_Test()
    {
        // Arrange
        using (var before = new FileAuditLog(_directory))
        {
            await before.RecordAsync(Event("before-restart", Day1), CancellationToken.None);
        }

        using var after = new FileAuditLog(_directory);

        // Act
        await after.RecordAsync(Event("after-restart", Day1), CancellationToken.None);

        // Assert
        var lines = await File.ReadAllLinesAsync(
            Path.Combine(_directory, Day1File),
            CancellationToken.None
        );
        Assert.Equal(2, lines.Length);
    }

    /// <summary>Verifies an IO failure (the "directory" is a file) is thrown to the caller rather than swallowed.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task RecordAsync_DirectoryPathIsAFile_ThrowsIOException_Test()
    {
        // Arrange
        Directory.CreateDirectory(_directory);
        var blocker = Path.Combine(_directory, "blocker");
        await File.WriteAllTextAsync(blocker, "not a directory", CancellationToken.None);
        using var sut = new FileAuditLog(blocker);

        // Act / Assert
        await Assert.ThrowsAnyAsync<IOException>(() =>
            sut.RecordAsync(Event("x", Day1), CancellationToken.None)
        );
    }

    /// <summary>Verifies a failed write does not wedge the lock: the next write to a good file still works.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task RecordAsync_AfterAFailedWrite_NextWriteSucceeds_Test()
    {
        // Arrange
        Directory.CreateDirectory(_directory);
        var sut = new FileAuditLog(Path.Combine(_directory, "file-blocks-this"));
        await File.WriteAllTextAsync(
            Path.Combine(_directory, "file-blocks-this"),
            "x",
            CancellationToken.None
        );
        using var good = new FileAuditLog(_directory);
        using (sut)
        {
            await Assert.ThrowsAnyAsync<IOException>(() =>
                sut.RecordAsync(Event("bad", Day1), CancellationToken.None)
            );
            await Assert.ThrowsAnyAsync<IOException>(() =>
                sut.RecordAsync(Event("bad-again", Day1), CancellationToken.None)
            );
        }

        // Act
        await good.RecordAsync(Event("good", Day1), CancellationToken.None);

        // Assert
        Assert.Single(
            await File.ReadAllLinesAsync(Path.Combine(_directory, Day1File), CancellationToken.None)
        );
    }

    /// <summary>Verifies a whitespace-only directory is rejected by the constructor.</summary>
    [Fact]
    public void Constructor_BlankDirectory_ThrowsArgumentException_Test()
    {
        // Arrange
        const string blank = " ";

        // Act / Assert
        var ex = Assert.Throws<ArgumentException>(() => Create(blank));
        Assert.Equal("directory", ex.ParamName);
    }

    /// <summary>Verifies a null directory is rejected by the constructor.</summary>
    [Fact]
    public void Constructor_NullDirectory_ThrowsArgumentNullException_Test()
    {
        // Arrange
        string? directory = null;

        // Act / Assert
        var ex = Assert.Throws<ArgumentNullException>(() => Create(directory!));
        Assert.Equal("directory", ex.ParamName);
    }

    /// <summary>Verifies a null event is rejected by <see cref="FileAuditLog.RecordAsync"/>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task RecordAsync_NullEvent_ThrowsArgumentNullException_Test()
    {
        // Arrange
        using var sut = new FileAuditLog(_directory);

        // Act / Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            sut.RecordAsync(null!, CancellationToken.None)
        );
    }

    private static void Create(string directory) => new FileAuditLog(directory).Dispose();

    private static AuditEvent Event(string action, DateTimeOffset timestamp) =>
        new()
        {
            Id = AnyEventId,
            Timestamp = timestamp,
            Action = action,
            Outcome = "ok",
        };

    private sealed class ManualClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
