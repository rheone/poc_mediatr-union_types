using System.Net;
using MediatrUnionPoc.Api.IntegrationTests.TestData;
using MediatrUnionPoc.Api.Proxies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Options;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>
/// Verifies the opt-in forwarded-headers handling through the rate limiter it exists for: with no trusted
/// proxy a client cannot choose the address it is limited (and audited) under, and with trusted proxies the
/// client address a proxy reports is honoured only from those proxies. Every request from the test host
/// reaches the API from <see cref="ProductsApiFactory.TestRemoteAddress"/>.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ForwardedHeadersTests : IDisposable
{
    private const string ForwardedFor = "X-Forwarded-For";
    private const string ClientA = "198.51.100.1";
    private const string ClientB = "198.51.100.2";
    private const string ClientC = "198.51.100.3";
    private const string ClientD = "198.51.100.9";

    private readonly ProductsApiFactory _factory = new();

    /// <inheritdoc/>
    public void Dispose() => _factory.Dispose();

    /// <summary>Verifies with no trusted proxy configured a spoofed X-Forwarded-For cannot change the rate-limit partition: every request shares the connection address's budget.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task NoTrustedProxies_SpoofedForwardedFor_CannotChangeThePartition_Test()
    {
        // Arrange
        using var factory = _factory.WithLimits(reads: 1);
        using var client = factory.CreateClient();

        // Act
        var statuses = new List<int>();
        foreach (var spoofed in new[] { ClientA, ClientB, ClientC })
        {
            using var response = await SendAsync(client, spoofed);
            statuses.Add((int)response.StatusCode);
        }

        // Assert
        Assert.Equal([401, 429, 429], statuses);
    }

    /// <summary>Verifies a trusted proxy's X-Forwarded-For becomes the client address: different forwarded clients get separate budgets, the same one shares its budget.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task TrustedProxy_ForwardedFor_PartitionsByTheForwardedClient_Test()
    {
        // Arrange
        using var factory = Trusting(ProductsApiFactory.TestRemoteAddress, reads: 1);
        using var client = factory.CreateClient();

        // Act
        var statuses = new List<int>();
        foreach (var forwarded in new[] { ClientA, ClientA, ClientB, ClientB })
        {
            using var response = await SendAsync(client, forwarded);
            statuses.Add((int)response.StatusCode);
        }

        // Assert
        Assert.Equal([401, 429, 401, 429], statuses);
    }

    /// <summary>Verifies a proxy given as a CIDR network is trusted as well.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task TrustedNetwork_ForwardedFor_IsHonoured_Test()
    {
        // Arrange
        using var factory = Trusting("203.0.113.0/24", reads: 1);
        using var client = factory.CreateClient();

        // Act
        using var first = await SendAsync(client, ClientA);
        using var other = await SendAsync(client, ClientB);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode),
            () => Assert.Equal(HttpStatusCode.Unauthorized, other.StatusCode)
        );
    }

    /// <summary>Verifies a forwarded header from an address that is not a trusted proxy is ignored, even when other proxies are trusted.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task UntrustedSender_ForwardedFor_IsIgnored_Test()
    {
        // Arrange
        using var factory = Trusting("192.0.2.1", reads: 1);
        using var client = factory.CreateClient();

        // Act
        var statuses = new List<int>();
        foreach (var spoofed in new[] { ClientA, ClientB })
        {
            using var response = await SendAsync(client, spoofed);
            statuses.Add((int)response.StatusCode);
        }

        // Assert
        Assert.Equal([401, 429], statuses);
    }

    /// <summary>Verifies the client address a trusted proxy reports is what the audit stream records as the source address.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task TrustedProxy_ForwardedFor_IsTheAuditSourceAddress_Test()
    {
        // Arrange
        using var factory = Trusting(ProductsApiFactory.TestRemoteAddress, impersonation: 1);
        using var client = factory.CreateClient();

        // Act
        using var first = await SendAsync(
            client,
            ClientD,
            ApiRoutes.ImpersonationTokens,
            HttpMethod.Post
        );
        using var refused = await SendAsync(
            client,
            ClientD,
            ApiRoutes.ImpersonationTokens,
            HttpMethod.Post
        );

        // Assert
        var refusal = Assert.Single(_factory.ReadAuditEvents());
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode),
            () => Assert.Equal(ClientD, (string?)refusal["sourceIp"])
        );
    }

    /// <summary>Verifies the defaults trust nobody.</summary>
    [Fact]
    public void Defaults_TrustNoProxy_Test()
    {
        // Arrange
        var options = new ApiForwardedHeadersOptions();

        // Act
        var trusted = options.TrustedProxies;

        // Assert
        Assert.Empty(trusted);
    }

    /// <summary>Verifies an IP address, an IPv6 address or a CIDR network is accepted.</summary>
    /// <param name="entry">A valid entry.</param>
    [Theory]
    [InlineData("10.0.0.5")]
    [InlineData("10.0.0.0/8")]
    [InlineData("203.0.113.0/24")]
    [InlineData("2001:db8::1")]
    [InlineData("2001:db8::/32")]
    public void Validate_ValidEntry_Passes_Test(string entry)
    {
        // Arrange / Act
        var result = Validate(entry);

        // Assert
        Assert.True(result.Succeeded);
    }

    /// <summary>Verifies malformed entries, catch-alls and unspecified addresses are refused.</summary>
    /// <param name="entry">An invalid entry.</param>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("	")]
    [InlineData("proxy.example.com")]
    [InlineData("10.0.0.0/33")]
    [InlineData("10.0.0.1/24")]
    [InlineData("10.0.0.0/abc")]
    [InlineData("0.0.0.0/0")]
    [InlineData("::/0")]
    [InlineData("0.0.0.0")]
    [InlineData("::")]
    [InlineData(" 10.0.0.5")]
    [InlineData("*")]
    public void Validate_InvalidEntry_Fails_Test(string entry)
    {
        // Arrange / Act
        var result = Validate(entry);

        // Assert
        Assert.True(result.Failed);
    }

    /// <summary>Verifies a host configured with a bad proxy entry refuses to start.</summary>
    [Fact]
    public void Start_InvalidTrustedProxy_FailsOptionsValidation_Test()
    {
        // Arrange
        using var factory = _factory.WithLimits(configure: builder =>
            builder.UseSetting("ForwardedHeaders:TrustedProxies:0", "not-an-ip")
        );

        // Act
        var exception = Record.Exception(() => factory.CreateClient().Dispose());

        // Assert
        Assert.IsType<OptionsValidationException>(exception);
    }

    private static ValidateOptionsResult Validate(string entry) =>
        new ApiForwardedHeadersOptionsValidator().Validate(
            null,
            new ApiForwardedHeadersOptions { TrustedProxies = [entry] }
        );

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        string forwardedFor,
        string uri = ApiRoutes.Products,
        HttpMethod? method = null
    )
    {
        using var request = new HttpRequestMessage(method ?? HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation(ForwardedFor, forwardedFor);
        return await client.SendAsync(request, CancellationToken.None);
    }

    private WebApplicationFactory<Program> Trusting(
        string proxy,
        int? reads = null,
        int? impersonation = null
    ) =>
        _factory.WithLimits(
            reads,
            impersonation: impersonation,
            configure: builder => builder.UseSetting("ForwardedHeaders:TrustedProxies:0", proxy)
        );
}
