namespace MediatrUnionPoc.Application.Features.Impersonation.IssueToken;

/// <summary>
/// The impersonation rules the Application layer needs (validation and the handler), without knowing
/// where they are configured: the host implements this over its options.
/// </summary>
public interface IImpersonationSettings
{
    /// <summary>Gets whether impersonation is switched on. When <see langword="false"/> the handler refuses every request.</summary>
    bool Enabled { get; }

    /// <summary>Gets the lifetime, in minutes, of a token whose request names none.</summary>
    int DefaultLifetimeMinutes { get; }

    /// <summary>Gets the longest lifetime, in minutes, a request may ask for.</summary>
    int MaxLifetimeMinutes { get; }

    /// <summary>Gets the roles an impersonation token may ever carry (compared ordinally); a requested role outside this set is refused.</summary>
    IReadOnlyCollection<string> AssignableRoles { get; }
}
