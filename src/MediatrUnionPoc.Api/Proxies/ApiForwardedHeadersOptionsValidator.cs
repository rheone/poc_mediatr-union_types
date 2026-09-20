using System.Net;
using Microsoft.Extensions.Options;

namespace MediatrUnionPoc.Api.Proxies;

/// <summary>
/// Validates <see cref="ApiForwardedHeadersOptions.TrustedProxies"/> on start: every entry must be an IP
/// address or a CIDR network, not the unspecified address and not a network that contains every address, so
/// a typo stops the host instead of silently trusting nobody (or everybody).
/// </summary>
public sealed class ApiForwardedHeadersOptionsValidator
    : IValidateOptions<ApiForwardedHeadersOptions>
{
    /// <summary>Parses one trusted-proxy entry.</summary>
    /// <param name="entry">The configured text.</param>
    /// <param name="address">The address, when the entry is a single address.</param>
    /// <param name="network">The network, when the entry is CIDR notation.</param>
    /// <returns>Whether the entry is acceptable; exactly one of the out values is set when it is.</returns>
    public static bool TryParse(string? entry, out IPAddress? address, out IPNetwork? network)
    {
        address = null;
        network = null;

        if (string.IsNullOrWhiteSpace(entry) || entry != entry.Trim())
        {
            return false;
        }

        if (IPAddress.TryParse(entry, out var single))
        {
            if (single.Equals(IPAddress.Any) || single.Equals(IPAddress.IPv6Any))
            {
                return false;
            }

            address = single;
            return true;
        }

        // A network whose address has host bits set (10.0.0.1/24) is a typo for 10.0.0.0/24: refuse it.
        var slash = entry.IndexOf('/', StringComparison.Ordinal);
        if (
            slash > 0
            && IPAddress.TryParse(entry.AsSpan(0, slash), out var written)
            && IPNetwork.TryParse(entry, out var parsed)
            && parsed.PrefixLength > 0
            && parsed.BaseAddress.Equals(written)
        )
        {
            network = parsed;
            return true;
        }

        return false;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public ValidateOptionsResult Validate(string? name, ApiForwardedHeadersOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];

        for (var index = 0; index < options.TrustedProxies.Length; index++)
        {
            if (!TryParse(options.TrustedProxies[index], out _, out _))
            {
                failures.Add(
                    $"{ApiForwardedHeadersOptions.SectionName}:TrustedProxies:{index} must be an IP address or a CIDR network such as 10.0.0.0/24, and not a catch-all."
                );
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
