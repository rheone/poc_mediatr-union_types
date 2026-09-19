using System.Runtime.CompilerServices;
using System.Security.Claims;
using MediatrUnionPoc.Application.Common.Abstractions;
using MediatrUnionPoc.Application.Common.Authorization;
using MediatrUnionPoc.Application.Common.Behaviors;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Tests.TestData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace MediatrUnionPoc.Application.Tests.Behaviors;

/// <summary>
/// A minimal command that opts into <see cref="AuthorizationBehavior{TRequest,TResponse}"/> via
/// <see cref="IRequiresAuthorization"/>, gated behind the <c>Administrator</c> policy — standing
/// in for a dev's own arbitrary command, never seen by the behavior's author, the same way
/// <c>ArbitraryCommand</c> stands in for <see cref="Common.Behaviors.TransactionBehavior{TRequest,TResponse}"/>
/// in <c>TransactionBehaviorTests</c>.
/// </summary>
/// <param name="Principal">The caller's identity, carried on the command itself.</param>
public sealed record ArbitraryAdminCommand(ClaimsPrincipal Principal)
    : ICommand<ArbitraryAdminOutcome>,
        IRequiresAuthorization
{
    /// <inheritdoc/>
    public string PolicyName => AuthorizationPolicies.Administrator;
}

/// <summary>The response union for <see cref="ArbitraryAdminCommand"/>.</summary>
public union ArbitraryAdminOutcome(Success, NotAuthorized) : IAuthorizable<ArbitraryAdminOutcome>
{
    /// <inheritdoc/>
    public static ArbitraryAdminOutcome FromNotAuthorized(NotAuthorized notAuthorized) =>
        notAuthorized;
}

/// <summary>
/// A minimal command gated behind a policy other than <c>Administrator</c> — proves
/// <see cref="AuthorizationBehavior{TRequest,TResponse}"/> reads the policy name off the request
/// generically rather than hardcoding one.
/// </summary>
/// <param name="Principal">The caller's identity, carried on the command itself.</param>
public sealed record ArbitraryPolicyCommand(ClaimsPrincipal Principal)
    : ICommand<ArbitraryAdminOutcome>,
        IRequiresAuthorization
{
    /// <summary>The name of a policy this test suite made up, distinct from <c>Administrator</c>.</summary>
    public const string SomeOtherPolicy = "SomeOtherPolicy";

    /// <inheritdoc/>
    public string PolicyName => SomeOtherPolicy;
}

/// <summary>
/// Verifies <see cref="AuthorizationBehavior{TRequest,TResponse}"/> gates a request behind the
/// <c>Administrator</c> policy: an authorized caller reaches the handler unchanged, an
/// unauthorized one is short-circuited to the union's <see cref="NotAuthorized"/> case without the
/// handler ever running.
/// </summary>
public sealed class AuthorizationBehaviorTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly AuthorizationBehavior<ArbitraryAdminCommand, ArbitraryAdminOutcome> _sut;

    /// <summary>Wires up <see cref="_sut"/> against a real <see cref="IAuthorizationService"/> backed by <see cref="AdministratorAuthorizationHandler"/>.</summary>
    public AuthorizationBehaviorTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore(options =>
            options.AddPolicy(
                AuthorizationPolicies.Administrator,
                policy => policy.Requirements.Add(new AdministratorRequirement("Administrator"))
            )
        );
        services.AddSingleton<IAuthorizationHandler, AdministratorAuthorizationHandler>();
        _provider = services.BuildServiceProvider();

        _sut = new AuthorizationBehavior<ArbitraryAdminCommand, ArbitraryAdminOutcome>(
            _provider.GetRequiredService<IAuthorizationService>(),
            NullLogger<AuthorizationBehavior<ArbitraryAdminCommand, ArbitraryAdminOutcome>>.Instance
        );
    }

    /// <inheritdoc/>
    public void Dispose() => _provider.Dispose();

    /// <summary>Verifies the handler runs and its response passes through unchanged when the caller is an administrator.</summary>
    /// <returns>A task that completes when the assertion runs.</returns>
    [Fact]
    public async Task Handle_administrator_caller_calls_next_and_passes_the_response_through()
    {
        // Arrange
        var command = new ArbitraryAdminCommand(PrincipalMother.WithRoles("Administrator"));
        ArbitraryAdminOutcome expected = new Success();

        // Act
        var result = await _sut.Handle(
            command,
            _ => Task.FromResult(expected),
            CancellationToken.None
        );

        // Assert
        Assert.Equal(expected, result);
    }

    /// <summary>Verifies the handler never runs and the response short-circuits to <see cref="NotAuthorized"/> when the caller isn't an administrator.</summary>
    /// <returns>A task that completes when the assertion runs.</returns>
    [Fact]
    public async Task Handle_non_administrator_caller_short_circuits_to_NotAuthorized_without_calling_next()
    {
        // Arrange
        var command = new ArbitraryAdminCommand(PrincipalMother.WithRoles("Viewer"));
        var nextWasCalled = false;

        // Act
        var result = await _sut.Handle(
            command,
            _ =>
            {
                nextWasCalled = true;
                return Task.FromResult<ArbitraryAdminOutcome>(new Success());
            },
            CancellationToken.None
        );

        // Assert
        Assert.False(nextWasCalled);
        var notAuthorized = Assert.IsType<NotAuthorized>(((IUnion)result).Value);
        Assert.Contains(AuthorizationPolicies.Administrator, Assert.Single(notAuthorized.Reasons));
    }

    // Auto Generated, verify expected behavior:
    /// <summary>Verifies the caller's cancellation token is forwarded to <c>next</c> rather than replaced.</summary>
    /// <returns>A task that completes when the assertion runs.</returns>
    [Fact]
    public async Task Handle_administrator_caller_forwards_the_cancellation_token_to_next()
    {
        // Arrange
        var command = new ArbitraryAdminCommand(PrincipalMother.WithRoles("Administrator"));
        using var cts = new CancellationTokenSource();
        CancellationToken? received = null;

        // Act
        await _sut.Handle(
            command,
            token =>
            {
                received = token;
                return Task.FromResult<ArbitraryAdminOutcome>(new Success());
            },
            cts.Token
        );

        // Assert
        Assert.Equal(cts.Token, received);
    }
}

/// <summary>
/// Verifies <see cref="AuthorizationBehavior{TRequest,TResponse}"/> reads the policy name off the
/// request itself — via <see cref="IRequiresAuthorization.PolicyName"/> — instead of hardcoding
/// <see cref="AuthorizationPolicies.Administrator"/>, using a mocked
/// <see cref="IAuthorizationService"/> so the exact policy name passed to
/// <see cref="Microsoft.AspNetCore.Authorization.AuthorizationServiceExtensions.AuthorizeAsync(IAuthorizationService,ClaimsPrincipal,string)"/>
/// can be asserted directly.
/// </summary>
public sealed class AuthorizationBehaviorPolicyNameTests
{
    private readonly IAuthorizationService _authorizationService =
        Substitute.For<IAuthorizationService>();

    private readonly AuthorizationBehavior<ArbitraryPolicyCommand, ArbitraryAdminOutcome> _sut;

    /// <summary>Wires up <see cref="_sut"/> against a mocked <see cref="IAuthorizationService"/>.</summary>
    public AuthorizationBehaviorPolicyNameTests() =>
        _sut = new AuthorizationBehavior<ArbitraryPolicyCommand, ArbitraryAdminOutcome>(
            _authorizationService,
            NullLogger<AuthorizationBehavior<ArbitraryPolicyCommand, ArbitraryAdminOutcome>>.Instance
        );

    /// <summary>
    /// Verifies the handler runs and its response passes through unchanged when
    /// <see cref="IAuthorizationService"/> succeeds for the request's own named policy — not
    /// <see cref="AuthorizationPolicies.Administrator"/>.
    /// </summary>
    /// <returns>A task that completes when the assertion runs.</returns>
    [Fact]
    public async Task Handle_named_policy_succeeds_calls_next_and_passes_the_response_through()
    {
        // Arrange
        var principal = PrincipalMother.Anonymous();
        var command = new ArbitraryPolicyCommand(principal);
        ArbitraryAdminOutcome expected = new Success();
        _authorizationService
            .AuthorizeAsync(principal, ArbitraryPolicyCommand.SomeOtherPolicy)
            .Returns(AuthorizationResult.Success());

        // Act
        var result = await _sut.Handle(
            command,
            _ => Task.FromResult(expected),
            CancellationToken.None
        );

        // Assert
        Assert.Equal(expected, result);
        await _authorizationService
            .Received(1)
            .AuthorizeAsync(principal, ArbitraryPolicyCommand.SomeOtherPolicy);
    }

    /// <summary>
    /// Verifies the handler never runs and the response short-circuits to
    /// <see cref="NotAuthorized"/> when <see cref="IAuthorizationService"/> fails for the
    /// request's own named policy — proving the behavior checked that policy, not a hardcoded one.
    /// </summary>
    /// <returns>A task that completes when the assertion runs.</returns>
    [Fact]
    public async Task Handle_named_policy_fails_short_circuits_to_NotAuthorized_without_calling_next()
    {
        // Arrange
        var principal = PrincipalMother.Anonymous();
        var command = new ArbitraryPolicyCommand(principal);
        var nextWasCalled = false;
        _authorizationService
            .AuthorizeAsync(principal, ArbitraryPolicyCommand.SomeOtherPolicy)
            .Returns(AuthorizationResult.Failed());

        // Act
        var result = await _sut.Handle(
            command,
            _ =>
            {
                nextWasCalled = true;
                return Task.FromResult<ArbitraryAdminOutcome>(new Success());
            },
            CancellationToken.None
        );

        // Assert
        Assert.False(nextWasCalled);
        var notAuthorized = Assert.IsType<NotAuthorized>(((IUnion)result).Value);
        Assert.Contains(
            ArbitraryPolicyCommand.SomeOtherPolicy,
            Assert.Single(notAuthorized.Reasons)
        );
        await _authorizationService
            .Received(1)
            .AuthorizeAsync(principal, ArbitraryPolicyCommand.SomeOtherPolicy);
        await _authorizationService
            .DidNotReceive()
            .AuthorizeAsync(principal, AuthorizationPolicies.Administrator);
    }
}
