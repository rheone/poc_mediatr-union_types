using System.Text.Json;
using System.Text.Json.Serialization;

namespace MediatrUnionPoc.Application.Common.Auditing;

/// <summary>
/// The canonical wire format of an <see cref="AuditEvent"/>: compact camelCase JSON with null members
/// omitted. Being compact JSON it is always a single line, and JSON escaping turns any line break or
/// quote inside free text into an escape sequence, so a reason cannot forge a second record. Every
/// <see cref="IAuditLog"/> that writes text should use it.
/// </summary>
public static class AuditEventJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    /// <summary>Serializes <paramref name="auditEvent"/> to one line of JSON (no trailing newline).</summary>
    /// <param name="auditEvent">The event.</param>
    /// <returns>The single-line JSON text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="auditEvent"/> is <see langword="null"/>.</exception>
    public static string Serialize(AuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        return JsonSerializer.Serialize(auditEvent, Options);
    }
}
