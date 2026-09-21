using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace MediatrUnionPoc.Api.Authentication;

/// <summary>
/// The development identity: when <see cref="UserId"/> is set, a request that carries no
/// <c>Authorization</c> header is signed in as that user with <see cref="Roles"/>, so the API can be
/// exercised (Scalar, <c>curl</c>) without minting a token. Bound from the <see cref="SectionName"/>
/// configuration section, read through <c>IOptionsMonitor</c> on every request so an edit to a
/// reloadable configuration file (<c>appsettings.Development.json</c>, or the git-ignored
/// <c>appsettings.Development.local.json</c>) takes effect without a restart, and validated on start
/// (per-field rules by the source-generated <see cref="DevIdentityOptionsValidator"/>, the
/// environment rule by <see cref="DevIdentityEnvironmentValidator"/>).
/// </summary>
/// <remarks>
/// Off by default: with <see cref="UserId"/> <see langword="null"/> nothing changes. It only ever
/// applies in the Development environment; a host in any other environment refuses to start with it
/// set, and ignores a value set after start.
/// </remarks>
public sealed class DevIdentityOptions
{
    /// <summary>The configuration section the options bind from.</summary>
    public const string SectionName = "Authentication:DevIdentity";

    /// <summary>The longest <see cref="UserId"/> accepted.</summary>
    public const int MaxUserIdLength = 200;

    /// <summary>
    /// Gets or sets the id requests are signed in as (the <c>NameIdentifier</c> claim, so the owner of
    /// what they create). <see langword="null"/> or blank leaves the development identity off, which is
    /// the default.
    /// </summary>
    [MaxLength(MaxUserIdLength)]
    public string? UserId { get; set; }

    /// <summary>
    /// Gets or sets the roles the development user holds (for example <c>Administrator</c>, or
    /// <c>Support</c>). <see langword="null"/> or empty means none. Role names are case-sensitive.
    /// </summary>
    [SuppressMessage(
        "Performance",
        "CA1819:Properties should not return arrays",
        Justification = "Configuration binding target; a settable array binds from a JSON array."
    )]
    public string[]? Roles { get; set; }

    /// <summary>Gets a value indicating whether a development user is configured.</summary>
    public bool IsActive => !string.IsNullOrWhiteSpace(UserId);
}
