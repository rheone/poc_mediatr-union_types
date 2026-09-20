using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using MediatrUnionPoc.Api.Authentication;
using MediatrUnionPoc.Application.Features.Impersonation.IssueToken;

namespace MediatrUnionPoc.Api.Impersonation;

/// <summary>
/// Settings for impersonation (minting a short-lived token that acts as another identity), registered
/// by <see cref="ImpersonationServiceCollectionExtensions.AddImpersonation"/>. Bound from the
/// <see cref="SectionName"/> configuration section and validated on start (per-field ranges by the
/// source-generated <see cref="ImpersonationOptionsValidator"/>, the cross-field rules by
/// <see cref="ImpersonationOptionsRules"/>), so a host that could mint tokens under a missing or
/// shared key refuses to start. Also the host's implementation of <see cref="IImpersonationSettings"/>.
/// </summary>
public sealed class ImpersonationOptions : IImpersonationSettings
{
    /// <summary>The configuration section the options bind from.</summary>
    public const string SectionName = "Impersonation";

    /// <summary>The largest lifetime, in minutes, either lifetime setting may have (one day).</summary>
    public const int LifetimeCeilingMinutes = 1440;

    /// <summary>
    /// Gets or sets the switch. When <see langword="false"/> the endpoint answers 404 to every
    /// authenticated caller, and tokens signed with <see cref="SigningKey"/> are no longer accepted by
    /// the bearer scheme, so switching it off also ends impersonation tokens already in circulation.
    /// Defaults to <see langword="true"/>.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the secret impersonation tokens are signed with (HMAC-SHA256, UTF-8 bytes), at
    /// least <see cref="JwtAuthOptions.MinimumSigningKeyLength"/> characters and different from the
    /// key ordinary tokens use; required while <see cref="Enabled"/>. Only
    /// <c>appsettings.Development.json</c> ships one, labelled development-only; supply it elsewhere
    /// through user secrets, an environment variable (<c>Impersonation__SigningKey</c>) or a secret
    /// store, never a committed file.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the lifetime, in minutes, of a token whose request names none. Not above <see cref="MaxLifetimeMinutes"/>. Defaults to 15.</summary>
    [Range(1, LifetimeCeilingMinutes)]
    public int DefaultLifetimeMinutes { get; set; } = 15;

    /// <summary>Gets or sets the longest lifetime, in minutes, a request may ask for; a longer request is a validation error. Defaults to 60.</summary>
    [Range(1, LifetimeCeilingMinutes)]
    public int MaxLifetimeMinutes { get; set; } = 60;

    /// <summary>Gets or sets the roles an impersonation token may ever carry. A role outside this list can never be granted, whoever asks. Empty by default (plain identities only).</summary>
    [SuppressMessage(
        "Performance",
        "CA1819:Properties should not return arrays",
        Justification = "Configuration binding target; a settable array binds from a JSON array."
    )]
    public string[] AssignableRoles { get; set; } = [];

    /// <inheritdoc/>
    IReadOnlyCollection<string> IImpersonationSettings.AssignableRoles => AssignableRoles;
}
