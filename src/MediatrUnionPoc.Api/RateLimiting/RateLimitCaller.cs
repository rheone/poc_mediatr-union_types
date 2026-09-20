using System.Security.Claims;
using MediatrUnionPoc.Application.Common.Authorization;

namespace MediatrUnionPoc.Api.RateLimiting;

/// <summary>
/// Who a request is counted against. An authenticated caller is counted by identity; an anonymous one
/// (no credentials, or a rejected token) by network address. The partition key is prefixed with its kind,
/// so an address string can never collide with a user id.
/// </summary>
/// <remarks>
/// A request made under an impersonation token is counted against the <em>real</em> actor (the token's
/// <c>act</c> subject), never the identity it runs as: otherwise an administrator could mint tokens for
/// many targets and spend a fresh budget under each. An impersonated principal whose actor cannot be read
/// falls back to the network address instead of the effective identity, for the same reason.
/// </remarks>
/// <param name="Kind">The kind of partition, <see cref="UserKind"/> or <see cref="IpKind"/>; safe to log.</param>
/// <param name="PartitionKey">The kind-prefixed key that identifies the partition; never logged or returned to a client.</param>
public readonly record struct RateLimitCaller(string Kind, string PartitionKey)
{
    /// <summary>The kind of an authenticated caller's partition.</summary>
    public const string UserKind = "user";

    /// <summary>The kind of an anonymous caller's partition.</summary>
    public const string IpKind = "ip";

    /// <summary>The address part of the key when the connection has no remote address (one shared bucket, not an exemption).</summary>
    public const string UnknownAddress = "unknown";

    /// <summary>Works out who <paramref name="context"/>'s request is counted against.</summary>
    /// <param name="context">The current request context; authentication must already have run.</param>
    /// <returns>The caller's partition.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static RateLimitCaller From(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var user = context.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            var id = user.IsImpersonated()
                ? user.GetActorId()
                : user.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!string.IsNullOrEmpty(id))
            {
                return new RateLimitCaller(UserKind, $"{UserKind}:{id}");
            }
        }

        var address = context.Connection.RemoteIpAddress;
        if (address is { IsIPv4MappedToIPv6: true })
        {
            address = address.MapToIPv4();
        }

        return new RateLimitCaller(IpKind, $"{IpKind}:{address?.ToString() ?? UnknownAddress}");
    }
}
