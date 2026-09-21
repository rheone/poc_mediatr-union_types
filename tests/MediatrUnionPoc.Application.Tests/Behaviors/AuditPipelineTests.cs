using System.Runtime.CompilerServices;
using MediatR;
using MediatrUnionPoc.Application.Common.Auditing;
using MediatrUnionPoc.Application.Common.Authorization;
using MediatrUnionPoc.Application.Features.Impersonation.IssueToken;
using MediatrUnionPoc.Application.Tests.TestData;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using static MediatrUnionPoc.Application.Tests.TestData.ImpersonationMother;

namespace MediatrUnionPoc.Application.Tests.Behaviors;

/// <summary>
/// Sends the impersonation command through the real, DI-built MediatR pipeline to prove the audit
/// behavior sits outside authorization and validation: a request those behaviors refuse, so its
/// handler never runs, is still recorded.
/// </summary>
public sealed class AuditPipelineTests : IDisposable
{
    private readonly RecordingAuditLog _log = new();
    private readonly ServiceProvider _provider;

    /// <summary>Builds the real pipeline over a recording audit log and a substituted token issuer.</summary>
    public AuditPipelineTests()
    {
        var issuer = Substitute.For<IImpersonationTokenIssuer>();
        issuer
            .Issue(Arg.Any<ImpersonationGrant>())
            .Returns(call =>
            {
                var grant = call.Arg<ImpersonationGrant>();
                return new ImpersonationToken(
                    "signed-token",
                    DateTimeOffset.UnixEpoch,
                    grant.TargetUserId,
                    grant.Roles,
                    grant.ActorId
                )
                {
                    TokenId = "jti-1",
                };
            });

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddSingleton<IAuditLog>(_log);
        services.AddSingleton(Settings());
        services.AddSingleton(issuer);
        _provider = services.BuildServiceProvider();
    }

    /// <summary>Disposes the service provider.</summary>
    public void Dispose() => _provider.Dispose();

    /// <summary>Verifies a caller the <c>Impersonator</c> policy refuses is recorded as NotAuthorized, attributed to the caller.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Send_PolicyRefusesCaller_IsStillAudited_Test()
    {
        // Arrange
        var plain = Caller("mallory", []);

        // Act
        var result = await SendAsync(Command(principal: plain, target: "victim"));

        // Assert
        var auditEvent = Assert.Single(_log.Events);
        Assert.Multiple(
            () => Assert.IsType<Common.Results.NotAuthorized>(((IUnion)result).Value),
            () => Assert.Equal("NotAuthorized", auditEvent.Outcome),
            () => Assert.Equal("mallory", auditEvent.ActorId),
            () => Assert.Equal("victim", auditEvent.TargetId),
            () =>
                Assert.Contains(
                    "Impersonator",
                    auditEvent.Details["denial"],
                    StringComparison.Ordinal
                )
        );
    }

    /// <summary>Verifies a request the validator refuses is recorded as ValidationErrors.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Send_ValidationFails_IsStillAudited_Test()
    {
        // Arrange
        var command = Command(reason: "short");

        // Act
        var result = await SendAsync(command);

        // Assert
        var auditEvent = Assert.Single(_log.Events);
        Assert.Multiple(
            () => Assert.IsType<Common.Results.ValidationErrors>(((IUnion)result).Value),
            () => Assert.Equal("ValidationErrors", auditEvent.Outcome),
            () => Assert.Equal(AdminId, auditEvent.ActorId),
            () =>
                Assert.Contains(
                    "Reason",
                    auditEvent.Details["validation"],
                    StringComparison.Ordinal
                )
        );
    }

    /// <summary>Verifies a mint that reaches the handler is recorded once, with the new token's id.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Send_TokenIssued_IsAuditedOnceWithTheTokenId_Test()
    {
        // Arrange
        var command = Command(ticket: "SUP-9");

        // Act
        await SendAsync(command);

        // Assert
        var auditEvent = Assert.Single(_log.Events);
        Assert.Multiple(
            () => Assert.Equal("ImpersonationToken", auditEvent.Outcome),
            () => Assert.Equal("jti-1", auditEvent.TokenId),
            () => Assert.Equal("SUP-9", auditEvent.Ticket)
        );
    }

    /// <summary>Verifies a fail-closed mint whose audit write fails surfaces an exception rather than a token.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public Task SendAsync_TokenIssuedButAuditFails_ThrowsInsteadOfReturningTheToken_Test()
    {
        // Arrange
        _log.FailWith = new IOException("read-only");
        var command = Command();

        // Act / Assert
        return Assert.ThrowsAsync<AuditWriteFailedException>(() => SendAsync(command));
    }

    private async Task<IssueImpersonationTokenResult> SendAsync(
        IssueImpersonationTokenCommand command
    )
    {
        using var scope = _provider.CreateScope();
        return await scope
            .ServiceProvider.GetRequiredService<ISender>()
            .Send(command, CancellationToken.None);
    }
}
