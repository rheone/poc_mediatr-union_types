using System.Security.Claims;
using FluentValidation;

namespace MediatrUnionPoc.Application.Features.Impersonation.IssueToken;

/// <summary>
/// Validates <see cref="IssueImpersonationTokenCommand"/> before <see cref="IssueImpersonationTokenHandler"/>
/// runs. Text is judged after trimming; the free-text members reject control characters (they are
/// written into the token and the audit log, where a line break would forge a log line); the lifetime
/// is checked against the configured maximum.
/// </summary>
public sealed class IssueImpersonationTokenValidator
    : AbstractValidator<IssueImpersonationTokenCommand>
{
    /// <summary>The longest a target user id may be.</summary>
    public const int MaxTargetUserIdLength = 200;

    /// <summary>The most roles one token may request.</summary>
    public const int MaxRoleCount = 10;

    /// <summary>The longest a role name may be.</summary>
    public const int MaxRoleLength = 64;

    /// <summary>The fewest characters (after trimming) a reason must have, so "test" is not a reason.</summary>
    public const int MinReasonLength = 10;

    /// <summary>The most characters (after trimming) a reason may have.</summary>
    public const int MaxReasonLength = 500;

    /// <summary>The most characters (after trimming) a ticket reference may have.</summary>
    public const int MaxTicketReferenceLength = 100;

    /// <summary>Builds the rules against the configured <paramref name="settings"/>.</summary>
    /// <param name="settings">Supplies the maximum lifetime a request may ask for.</param>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is <see langword="null"/>.</exception>
    public IssueImpersonationTokenValidator(IImpersonationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        RuleFor(x => x.TargetUserId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(id => id!.Trim().Length <= MaxTargetUserIdLength)
            .WithMessage($"'Target User Id' must be at most {MaxTargetUserIdLength} characters.")
            .Must(HasNoControlCharacters)
            .WithMessage("'Target User Id' must not contain control characters.")
            .Must((command, id) => !IsCaller(command.Principal, id!))
            .WithMessage(
                "'Target User Id' must differ from the caller's own id: impersonating yourself is pointless."
            );

        RuleFor(x => x.Roles)
            .Must(roles => roles!.Count <= MaxRoleCount)
            .WithMessage($"'Roles' must have at most {MaxRoleCount} entries.")
            .When(x => x.Roles is not null);
        RuleForEach(x => x.Roles)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(role => role.Trim().Length <= MaxRoleLength)
            .WithMessage($"Each role must be at most {MaxRoleLength} characters.")
            .Must(HasNoControlCharacters)
            .WithMessage("A role must not contain control characters.")
            .When(x => x.Roles is not null);

        RuleFor(x => x.Reason)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(reason => reason!.Trim().Length >= MinReasonLength)
            .WithMessage($"'Reason' must be at least {MinReasonLength} characters.")
            .Must(reason => reason!.Trim().Length <= MaxReasonLength)
            .WithMessage($"'Reason' must be at most {MaxReasonLength} characters.")
            .Must(HasNoControlCharacters)
            .WithMessage("'Reason' must not contain control characters.");

        RuleFor(x => x.TicketReference)
            .Must(ticket => ticket!.Trim().Length <= MaxTicketReferenceLength)
            .WithMessage(
                $"'Ticket Reference' must be at most {MaxTicketReferenceLength} characters."
            )
            .Must(HasNoControlCharacters)
            .WithMessage("'Ticket Reference' must not contain control characters.")
            .When(x => x.TicketReference is not null);

        RuleFor(x => x.LifetimeMinutes)
            .GreaterThan(0)
            .LessThanOrEqualTo(settings.MaxLifetimeMinutes)
            .WithMessage($"'Lifetime Minutes' must be between 1 and {settings.MaxLifetimeMinutes}.")
            .When(x => x.LifetimeMinutes is not null);
    }

    private static bool HasNoControlCharacters(string? value) =>
        value is null || !value.Any(char.IsControl);

    private static bool IsCaller(ClaimsPrincipal principal, string targetUserId) =>
        string.Equals(
            principal.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            targetUserId.Trim(),
            StringComparison.Ordinal
        );
}
