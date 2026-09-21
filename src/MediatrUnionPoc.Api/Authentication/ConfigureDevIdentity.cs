using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MediatrUnionPoc.Api.Authentication;

/// <summary>
/// Wires the development identity into the JWT bearer handler: when a request carries no
/// <c>Authorization</c> header and <see cref="DevIdentityOptions"/> names a user (Development only),
/// the bearer handler succeeds with a principal for that user instead of leaving the request
/// anonymous. A request that does send an <c>Authorization</c> header is judged as usual, so a real
/// or invalid token is never replaced.
/// </summary>
/// <remarks>
/// The options are read through <see cref="IOptionsMonitor{TOptions}"/> per request, so a reloaded
/// configuration file changes who a tokenless request is, or turns it off, immediately. The principal
/// carries <see cref="ClaimTypes.NameIdentifier"/> and <see cref="ClaimTypes.Role"/> directly, the
/// types the Application layer authorizes against (the same types the bearer handler's inbound claim
/// mapping produces for a token's <c>sub</c> and <c>role</c>).
/// </remarks>
/// <param name="options">The development identity settings, monitored for changes.</param>
/// <param name="environment">The host environment.</param>
/// <param name="logger">The logger a sign-in is reported to.</param>
/// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
public sealed partial class ConfigureDevIdentity(
    IOptionsMonitor<DevIdentityOptions> options,
    IHostEnvironment environment,
    ILogger<ConfigureDevIdentity> logger
) : IConfigureNamedOptions<JwtBearerOptions>
{
    /// <summary>The authentication type of the development principal's identity.</summary>
    public const string AuthenticationType = "DevIdentity";

    private readonly IOptionsMonitor<DevIdentityOptions> _options =
        options ?? throw new ArgumentNullException(nameof(options));

    private readonly IHostEnvironment _environment =
        environment ?? throw new ArgumentNullException(nameof(environment));

    private readonly ILogger<ConfigureDevIdentity> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc/>
    public void Configure(JwtBearerOptions options) =>
        Configure(JwtBearerDefaults.AuthenticationScheme, options);

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public void Configure(string? name, JwtBearerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (name != JwtBearerDefaults.AuthenticationScheme)
        {
            return;
        }

        options.Events ??= new JwtBearerEvents();
        var inner = options.Events.OnMessageReceived;
        options.Events.OnMessageReceived = async context =>
        {
            await inner(context);
            if (
                context.Result is null
                && context.Principal is null
                && !context.Request.Headers.ContainsKey("Authorization")
                && TryCreatePrincipal(out var principal)
            )
            {
                context.Principal = principal;
                context.Success();
            }
        };
    }

    private bool TryCreatePrincipal(out ClaimsPrincipal principal)
    {
        principal = null!;
        var current = _options.CurrentValue;
        if (!current.IsActive || !_environment.IsDevelopment())
        {
            return false;
        }

        var userId = current.UserId!.Trim();
        var identity = new ClaimsIdentity(AuthenticationType, ClaimTypes.Name, ClaimTypes.Role);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId));
        identity.AddClaim(new Claim(ClaimTypes.Name, userId));
        foreach (var role in (current.Roles ?? []).Where(role => !string.IsNullOrWhiteSpace(role)))
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        LogSignedIn(_logger, userId);
        principal = new ClaimsPrincipal(identity);
        return true;
    }

    [LoggerMessage(
        EventId = 1500,
        EventName = "DevIdentitySignedIn",
        Level = LogLevel.Warning,
        Message = "A request without a token was signed in as the development user {DevUserId} (Authentication:DevIdentity)"
    )]
    private static partial void LogSignedIn(ILogger logger, string devUserId);
}
