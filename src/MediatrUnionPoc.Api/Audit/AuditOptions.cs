using System.ComponentModel.DataAnnotations;

namespace MediatrUnionPoc.Api.Audit;

/// <summary>
/// Settings for the audit stream registered by <see cref="AuditServiceCollectionExtensions.AddAudit"/>.
/// Bound from the <see cref="SectionName"/> configuration section and validated on start (by the
/// source-generated <see cref="AuditOptionsValidator"/>). There is deliberately no switch to turn
/// auditing off: the impersonation endpoint is a controlled authentication bypass and must not
/// operate unrecorded. The application never deletes an audit file; retention (archiving, expiry) is
/// an operational decision.
/// </summary>
public sealed class AuditOptions
{
    /// <summary>The configuration section the options bind from.</summary>
    public const string SectionName = "Audit";

    /// <summary>The directory used when none is configured, relative to the content root.</summary>
    public const string DefaultDirectory = "logs/audit";

    /// <summary>
    /// Gets or sets the directory the daily <c>audit-yyyyMMdd.jsonl</c> files are written to. A relative
    /// path is resolved against the content root; the directory is created on the first write.
    /// Defaults to <see cref="DefaultDirectory"/>. Keep it out of the operational log's directory and
    /// give it stricter permissions and its own backup and retention.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string Directory { get; set; } = DefaultDirectory;
}
