using MediatrUnionPoc.Api.Authentication;
using Microsoft.Extensions.Options;

namespace MediatrUnionPoc.Api.Impersonation;

/// <summary>
/// The <see cref="ImpersonationOptions"/> rules DataAnnotations cannot express: while impersonation
/// is enabled it needs a signing key of sufficient length that is not the ordinary token key (the
/// separate key is the point: a leaked ordinary key must not also be the impersonation key, and the
/// two kinds of token stay distinguishable by who can sign them), and the default lifetime may not
/// exceed the maximum.
/// </summary>
/// <param name="configuration">Read for the ordinary signing key, so this check does not depend on that key's own options validating first.</param>
public sealed class ImpersonationOptionsRules(IConfiguration configuration)
    : IValidateOptions<ImpersonationOptions>
{
    private readonly IConfiguration _configuration =
        configuration ?? throw new ArgumentNullException(nameof(configuration));

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public ValidateOptionsResult Validate(string? name, ImpersonationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];

        if (options.DefaultLifetimeMinutes > options.MaxLifetimeMinutes)
        {
            failures.Add(
                "Impersonation:DefaultLifetimeMinutes must not exceed Impersonation:MaxLifetimeMinutes."
            );
        }

        if (options.Enabled)
        {
            if (options.SigningKey.Length < JwtAuthOptions.MinimumSigningKeyLength)
            {
                failures.Add(
                    $"Impersonation:SigningKey must be at least {JwtAuthOptions.MinimumSigningKeyLength} characters while impersonation is enabled."
                );
            }
            else if (
                string.Equals(
                    options.SigningKey,
                    _configuration[$"{JwtAuthOptions.SectionName}:SigningKey"],
                    StringComparison.Ordinal
                )
            )
            {
                failures.Add(
                    "Impersonation:SigningKey must differ from Authentication:Jwt:SigningKey."
                );
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
