using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MediatrUnionPoc.Api.Authentication;

/// <summary>
/// Refuses to start a host outside Development whose <see cref="DevIdentityOptions"/> names a user, so
/// a development identity can never be switched on by a settings file that reaches another environment.
/// </summary>
/// <param name="environment">The host environment.</param>
/// <exception cref="ArgumentNullException"><paramref name="environment"/> is <see langword="null"/>.</exception>
public sealed class DevIdentityEnvironmentValidator(IHostEnvironment environment)
    : IValidateOptions<DevIdentityOptions>
{
    private readonly IHostEnvironment _environment =
        environment ?? throw new ArgumentNullException(nameof(environment));

    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, DevIdentityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.IsActive && !_environment.IsDevelopment()
            ? ValidateOptionsResult.Fail(
                $"{DevIdentityOptions.SectionName}:UserId is set, but the development identity is only allowed in the Development environment (this host is '{_environment.EnvironmentName}'). Remove it."
            )
            : ValidateOptionsResult.Success;
    }
}
