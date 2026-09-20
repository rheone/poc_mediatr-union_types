using System.Globalization;
using System.Text;
using MediatrUnionPoc.Application.Common.Auditing;

namespace MediatrUnionPoc.Api.Audit;

/// <summary>
/// The audit stream as files: JSON Lines (one <see cref="AuditEventJson"/> record per line, UTF-8, LF),
/// one file per UTC day named <c>audit-yyyyMMdd.jsonl</c>, chosen by the event's own timestamp.
/// Appends are serialized by a lock so concurrent requests never interleave bytes, and every event is
/// written through to disk before <see cref="RecordAsync"/> completes. A failure to write throws: this
/// implementation never swallows an IO error, because the caller's
/// <see cref="AuditFailurePolicy"/> decides what a lost record means. Files are only ever appended to;
/// nothing here deletes or rewrites one.
/// </summary>
/// <remarks>
/// This is a single-process writer. Several instances sharing one directory append whole lines and
/// are unlikely to interleave, but that is not guaranteed by the file system on every platform.
/// </remarks>
public sealed class FileAuditLog : IAuditLog, IDisposable
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string _directory;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Initializes a new instance of the <see cref="FileAuditLog"/> class.</summary>
    /// <param name="directory">The directory the daily files are written to (created on the first write).</param>
    /// <exception cref="ArgumentException"><paramref name="directory"/> is null or white space.</exception>
    public FileAuditLog(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        _directory = directory;
    }

    /// <summary>Gets the name of the file an event with <paramref name="timestamp"/> is appended to.</summary>
    /// <param name="timestamp">The event's timestamp.</param>
    /// <returns>The file name (no directory).</returns>
    public static string FileNameFor(DateTimeOffset timestamp) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"audit-{timestamp.UtcDateTime:yyyyMMdd}.jsonl"
        );

    /// <inheritdoc/>
    /// <exception cref="IOException">The directory or file could not be created or written.</exception>
    /// <exception cref="UnauthorizedAccessException">The process may not write there.</exception>
    public async Task RecordAsync(
        AuditEvent auditEvent,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        var bytes = Utf8.GetBytes(AuditEventJson.Serialize(auditEvent) + "\n");
        var path = Path.Combine(_directory, FileNameFor(auditEvent.Timestamp));

        await _gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(_directory);

            var stream = new FileStream(
                path,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 1,
                FileOptions.Asynchronous | FileOptions.WriteThrough
            );
            await using (stream)
            {
                await stream.WriteAsync(bytes, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc/>
    public void Dispose() => _gate.Dispose();
}
