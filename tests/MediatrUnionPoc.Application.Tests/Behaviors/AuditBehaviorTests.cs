using System.Runtime.CompilerServices;
using MediatR;
using MediatrUnionPoc.Application.Common.Auditing;
using MediatrUnionPoc.Application.Common.Authorization;
using MediatrUnionPoc.Application.Common.Behaviors;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Features.Impersonation.IssueToken;
using MediatrUnionPoc.Application.Features.Products.Common;
using MediatrUnionPoc.Application.Features.Products.Create;
using MediatrUnionPoc.Application.Features.Products.Delete;
using MediatrUnionPoc.Application.Tests.TestData;
using MediatrUnionPoc.Domain;
using Microsoft.Extensions.Logging;
using static MediatrUnionPoc.Application.Tests.TestData.ImpersonationMother;

namespace MediatrUnionPoc.Application.Tests.Behaviors;

/// <summary>
/// Tests <see cref="AuditBehavior{TRequest,TResponse}"/> with real auditable requests and a recording
/// <see cref="IAuditLog"/>: the outcome named for every union case, who is recorded as the actor, what
/// the request says about its target, the two failure policies and that a token never reaches the event.
/// </summary>
public sealed class AuditBehaviorTests
{
    private const string Secret = "the-signed-token-value-that-must-never-be-audited";
    private static readonly Guid DeletedProductId = new("c4a81f36-5d27-4e90-b3a6-7f2e19d08b54");
    private static readonly DateTimeOffset Now = new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

    private readonly RecordingAuditLog _log = new();
    private readonly CapturingLogger<
        AuditBehavior<IssueImpersonationTokenCommand, IssueImpersonationTokenResult>
    > _issueLogger = new();

    /// <summary>Verifies each union case of the impersonation result is recorded under its own case name.</summary>
    /// <param name="caseName">The expected outcome.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData("ImpersonationToken")]
    [InlineData("ValidationErrors")]
    [InlineData("NotAuthorized")]
    [InlineData("Error")]
    public async Task Handle_EachUnionCase_RecordsTheCaseNameAsTheOutcome_Test(string caseName)
    {
        // Arrange
        IssueImpersonationTokenResult response = caseName switch
        {
            "ImpersonationToken" => Token(),
            "ValidationErrors" => new ValidationErrors([new ValidationError("Reason", "required")]),
            "NotAuthorized" => new NotAuthorized(["no"]),
            _ => new Error("off", ImpersonationErrors.DisabledCode),
        };

        // Act
        await IssueSut().Handle(Command(), Next(response), CancellationToken.None);

        // Assert
        Assert.Equal(caseName, Assert.Single(_log.Events).Outcome);
    }

    /// <summary>Verifies the event carries a fresh id, the injected clock's time, the action, and the context's trace id and source address.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_AnyOutcome_StampsIdTimeActionAndCorrelation_Test()
    {
        // Arrange
        var command = Command();
        var next = NextIssue(Token());

        // Act
        await IssueSut().Handle(command, next, CancellationToken.None);

        // Assert
        var auditEvent = Assert.Single(_log.Events);
        Assert.Multiple(
            () => Assert.NotEqual(Guid.Empty, auditEvent.Id),
            () => Assert.Equal(Now, auditEvent.Timestamp),
            () => Assert.Equal("Impersonation.IssueToken", auditEvent.Action),
            () => Assert.Equal("trace-1", auditEvent.TraceId),
            () => Assert.Equal("203.0.113.7", auditEvent.SourceIp)
        );
    }

    /// <summary>Verifies a plain caller is both the actor and the effective identity, and is not flagged impersonated.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_PlainCaller_ActorAndEffectiveAreTheCaller_Test()
    {
        // Arrange
        var command = Command(principal: Caller(AdminId, ["Administrator"]));

        // Act
        await IssueSut().Handle(command, NextIssue(Token()), CancellationToken.None);

        // Assert
        var auditEvent = Assert.Single(_log.Events);
        Assert.Multiple(
            () => Assert.Equal(AdminId, auditEvent.ActorId),
            () => Assert.Equal(AdminId, auditEvent.EffectiveId),
            () => Assert.False(auditEvent.IsImpersonated)
        );
    }

    /// <summary>Verifies an impersonated principal is attributed to the real caller in its <c>act</c> claim, with the effective identity and the token id kept separately.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_ImpersonatedCaller_ActorIsTheActClaimSubject_Test()
    {
        // Arrange
        var chained = Caller(
            "alice",
            ["Administrator"],
            marker: true,
            actor: AdminId,
            tokenId: "jti-of-the-used-token"
        );

        // Act
        await IssueSut()
            .Handle(
                Command(principal: chained),
                NextIssue(new NotAuthorized(["chained"])),
                CancellationToken.None
            );

        // Assert
        var auditEvent = Assert.Single(_log.Events);
        Assert.Multiple(
            () => Assert.Equal(AdminId, auditEvent.ActorId),
            () => Assert.Equal("alice", auditEvent.EffectiveId),
            () => Assert.True(auditEvent.IsImpersonated),
            () => Assert.Equal("jti-of-the-used-token", auditEvent.TokenId)
        );
    }

    /// <summary>Verifies a mint records the target, trimmed reason and ticket, the granted roles and the new token's id, and never the token string.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_IssuedToken_RecordsTargetReasonRolesAndTokenIdButNotTheToken_Test()
    {
        // Arrange
        var command = Command(target: " alice ", reason: $" {ValidReason} ", ticket: " SUP-3 ");

        // Act
        await IssueSut().Handle(command, NextIssue(Token("Support")), CancellationToken.None);

        // Assert
        var auditEvent = Assert.Single(_log.Events);
        Assert.Multiple(
            () => Assert.Equal("User", auditEvent.TargetType),
            () => Assert.Equal("alice", auditEvent.TargetId),
            () => Assert.Equal(ValidReason, auditEvent.Reason),
            () => Assert.Equal("SUP-3", auditEvent.Ticket),
            () => Assert.Equal("Support", auditEvent.Details["roles"]),
            () => Assert.Equal("jti-new", auditEvent.TokenId),
            () =>
                Assert.DoesNotContain(
                    Secret,
                    AuditEventJson.Serialize(auditEvent),
                    StringComparison.Ordinal
                )
        );
    }

    /// <summary>Verifies a refusal records the denial message and the roles that were asked for.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_Refusal_RecordsTheDenialAndTheRequestedRoles_Test()
    {
        // Arrange
        var command = Command(roles: ["SuperUser"]);

        // Act
        await IssueSut()
            .Handle(
                command,
                NextIssue(
                    new NotAuthorized(["Roles that impersonation may not grant: SuperUser."])
                ),
                CancellationToken.None
            );

        // Assert
        var auditEvent = Assert.Single(_log.Events);
        Assert.Multiple(
            () => Assert.Equal("SuperUser", auditEvent.Details["roles"]),
            () =>
                Assert.Contains(
                    "SuperUser",
                    auditEvent.Details["denial"],
                    StringComparison.Ordinal
                ),
            () => Assert.Null(auditEvent.TokenId)
        );
    }

    /// <summary>Verifies free text is bounded, so unvalidated input cannot bloat a record.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_OversizedReason_IsTruncated_Test()
    {
        // Arrange
        var command = Command(reason: new string('x', 100_000));

        // Act
        await IssueSut()
            .Handle(
                command,
                NextIssue(new ValidationErrors([new ValidationError("Reason", "too long")])),
                CancellationToken.None
            );

        // Assert
        Assert.Equal(AuditText.MaxLength, Assert.Single(_log.Events).Reason!.Length);
    }

    /// <summary>Verifies a fail-closed request whose event cannot be written throws, logs at Error, and the handler's response is not returned.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_FailClosedAndLogThrows_ThrowsAuditWriteFailedAndLogsError_Test()
    {
        // Arrange
        var failure = new IOException("disk full");
        _log.FailWith = failure;

        // Act
        var thrown = await Assert.ThrowsAsync<AuditWriteFailedException>(() =>
            IssueSut().Handle(Command(), NextIssue(Token()), CancellationToken.None)
        );

        // Assert
        var entry = Assert.Single(_issueLogger.Entries);
        Assert.Multiple(
            () => Assert.Same(failure, thrown.InnerException),
            () => Assert.Equal(LogLevel.Error, entry.Level),
            () => Assert.Same(failure, entry.Exception),
            () => Assert.DoesNotContain(Secret, entry.Message, StringComparison.Ordinal)
        );
    }

    /// <summary>Verifies a best-effort request whose event cannot be written still returns its response, and the failure is logged at Error.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_BestEffortAndLogThrows_ReturnsResponseAndLogsError_Test()
    {
        // Arrange
        _log.FailWith = new IOException("disk full");
        var logger =
            new CapturingLogger<AuditBehavior<CreateProductCommand, CreateProductResult>>();
        var sut = new AuditBehavior<CreateProductCommand, CreateProductResult>(
            _log,
            new StubAuditRequestContext(),
            new FixedTimeProvider(Now),
            logger
        );
        CreateProductResult response = Dto();

        // Act
        var result = await sut.Handle(
            new CreateProductCommand("Widget", 1m, PrincipalMother.WithId("alice")),
            Next(response),
            CancellationToken.None
        );

        // Assert
        Assert.Multiple(
            () => Assert.IsType<ProductDto>(((IUnion)result).Value),
            () => Assert.Equal(LogLevel.Error, Assert.Single(logger.Entries).Level)
        );
    }

    /// <summary>Verifies the write ignores a cancelled request token, so a client that disconnects cannot cost a committed action its record.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_RequestCancelled_StillWritesWithoutTheRequestToken_Test()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        await IssueSut().Handle(Command(), NextIssue(Token()), cts.Token);

        // Assert
        Assert.Multiple(
            () => Assert.Single(_log.Events),
            () => Assert.False(_log.LastToken.CanBeCanceled)
        );
    }

    /// <summary>Verifies an exception from the rest of the pipeline is recorded as an <c>Exception</c> outcome and rethrown unchanged, even when the audit write also fails.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_PipelineThrows_RecordsExceptionOutcomeAndRethrowsTheOriginal_Test()
    {
        // Arrange
        var original = new InvalidOperationException("boom");

        // Act
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            IssueSut()
                .Handle(
                    Command(),
                    _ => Task.FromException<IssueImpersonationTokenResult>(original),
                    CancellationToken.None
                )
        );
        _log.FailWith = new IOException("disk full");
        var stillOriginal = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            IssueSut()
                .Handle(
                    Command(),
                    _ => Task.FromException<IssueImpersonationTokenResult>(original),
                    CancellationToken.None
                )
        );

        // Assert
        var auditEvent = Assert.Single(_log.Events);
        Assert.Multiple(
            () =>
                Assert.Equal(
                    AuditBehavior<
                        IssueImpersonationTokenCommand,
                        IssueImpersonationTokenResult
                    >.UnhandledOutcome,
                    auditEvent.Outcome
                ),
            () => Assert.Equal("alice", auditEvent.TargetId),
            () => Assert.Same(original, thrown),
            () => Assert.Same(original, stillOriginal)
        );
    }

    /// <summary>Verifies a successful create is recorded against the id of the product the response carries, since the request had no id to name.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_CreateSucceeds_TargetIsTheCreatedProductsId_Test()
    {
        // Arrange
        var dto = Dto();
        CreateProductResult response = dto;

        // Act
        await CreateSut()
            .Handle(
                new CreateProductCommand("Widget", 1m, PrincipalMother.WithId("alice")),
                Next(response),
                CancellationToken.None
            );

        // Assert
        var auditEvent = Assert.Single(_log.Events);
        Assert.Multiple(
            () => Assert.Equal("Product.Create", auditEvent.Action),
            () => Assert.Equal("ProductDto", auditEvent.Outcome),
            () => Assert.Equal("Product", auditEvent.TargetType),
            () => Assert.Equal(dto.Id.Value.ToString(), auditEvent.TargetId),
            () => Assert.Equal("alice", auditEvent.ActorId)
        );
    }

    /// <summary>Verifies a create that produced no product, and one with no principal at all, are recorded without a target or an actor.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_CreateRefusedWithoutPrincipal_HasNoTargetAndNoActor_Test()
    {
        // Arrange
        CreateProductResult response = new Conflict("name taken");

        // Act
        await CreateSut()
            .Handle(new CreateProductCommand("Widget", 1m), Next(response), CancellationToken.None);

        // Assert
        var auditEvent = Assert.Single(_log.Events);
        Assert.Multiple(
            () => Assert.Equal("Conflict", auditEvent.Outcome),
            () => Assert.Null(auditEvent.TargetId),
            () => Assert.Null(auditEvent.ActorId),
            () => Assert.Null(auditEvent.EffectiveId)
        );
    }

    /// <summary>Verifies a delete is recorded against the requested id whatever the outcome, and a refusal carries its denial.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_DeleteRefused_RecordsTheRequestedIdAndTheDenial_Test()
    {
        // Arrange
        var id = DeletedProductId;
        var sut = new AuditBehavior<DeleteProductCommand, DeleteProductResult>(
            _log,
            new StubAuditRequestContext(),
            new FixedTimeProvider(Now),
            new CapturingLogger<AuditBehavior<DeleteProductCommand, DeleteProductResult>>()
        );
        DeleteProductResult response = new NotAuthorized(["not an administrator"]);

        // Act
        await sut.Handle(
            new DeleteProductCommand(id, PrincipalMother.WithId("bob")),
            Next(response),
            CancellationToken.None
        );

        // Assert
        var auditEvent = Assert.Single(_log.Events);
        Assert.Multiple(
            () => Assert.Equal("Product.Delete", auditEvent.Action),
            () => Assert.Equal("NotAuthorized", auditEvent.Outcome),
            () => Assert.Equal(id.ToString(), auditEvent.TargetId),
            () => Assert.Equal("not an administrator", auditEvent.Details["denial"])
        );
    }

    /// <summary>Verifies the constructor rejects null dependencies.</summary>
    [Fact]
    public void Ctor_NullDependencies_ThrowArgumentNullException_Test()
    {
        // Arrange
        var context = new StubAuditRequestContext();
        var clock = new FixedTimeProvider(Now);

        // Act
        var names = new[]
        {
            Assert
                .Throws<ArgumentNullException>(() => Build(null!, context, clock, _issueLogger))
                .ParamName,
            Assert
                .Throws<ArgumentNullException>(() => Build(_log, null!, clock, _issueLogger))
                .ParamName,
            Assert
                .Throws<ArgumentNullException>(() => Build(_log, context, null!, _issueLogger))
                .ParamName,
            Assert
                .Throws<ArgumentNullException>(() => Build(_log, context, clock, null!))
                .ParamName,
        };

        // Assert
        Assert.Equal(["auditLog", "context", "clock", "logger"], names.Select(name => name!));
    }

    private static AuditBehavior<
        IssueImpersonationTokenCommand,
        IssueImpersonationTokenResult
    > Build(
        IAuditLog log,
        IAuditRequestContext context,
        TimeProvider clock,
        ILogger<AuditBehavior<IssueImpersonationTokenCommand, IssueImpersonationTokenResult>> logger
    ) => new(log, context, clock, logger);

    private AuditBehavior<
        IssueImpersonationTokenCommand,
        IssueImpersonationTokenResult
    > IssueSut() =>
        Build(_log, new StubAuditRequestContext(), new FixedTimeProvider(Now), _issueLogger);

    private AuditBehavior<CreateProductCommand, CreateProductResult> CreateSut() =>
        new(
            _log,
            new StubAuditRequestContext(),
            new FixedTimeProvider(Now),
            new CapturingLogger<AuditBehavior<CreateProductCommand, CreateProductResult>>()
        );

    private static RequestHandlerDelegate<T> Next<T>(T response) => _ => Task.FromResult(response);

    private static RequestHandlerDelegate<IssueImpersonationTokenResult> NextIssue(
        IssueImpersonationTokenResult response
    ) => _ => Task.FromResult(response);

    private static ImpersonationToken Token(params string[] roles) =>
        new(Secret, Now.AddMinutes(15), TargetId, roles, AdminId) { TokenId = "jti-new" };

    private static ProductDto Dto() =>
        new(ProductId.New(), "Widget", 1m, ProductVersion.Initial, Now);
}
