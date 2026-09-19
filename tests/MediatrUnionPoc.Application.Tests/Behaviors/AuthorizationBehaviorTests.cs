using System.Security.Claims;
using MediatrUnionPoc.Application.Common.Abstractions;
using MediatrUnionPoc.Application.Common.Authorization;
using MediatrUnionPoc.Application.Common.Behaviors;
using MediatrUnionPoc.Application.Common.Results;
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
    public async Task Calls_next_and_passes_the_response_through_when_the_caller_is_an_administrator()
    {
        var command = new ArbitraryAdminCommand(PrincipalWithRoles("Administrator"));
        ArbitraryAdminOutcome expected = new Success();

        var result = await _sut.Handle(
            command,
            _ => Task.FromResult(expected),
            CancellationToken.None
        );

        Assert.Equal(expected, result);
    }

    /// <summary>Verifies the handler never runs and the response short-circuits to <see cref="NotAuthorized"/> when the caller isn't an administrator.</summary>
    /// <returns>A task that completes when the assertion runs.</returns>
    [Fact]
    public async Task Short_circuits_to_NotAuthorized_without_calling_next_when_the_caller_is_not_an_administrator()
    {
        var command = new ArbitraryAdminCommand(PrincipalWithRoles("Viewer"));
        var nextWasCalled = false;

        var result = await _sut.Handle(
            command,
            _ =>
            {
                nextWasCalled = true;
                return Task.FromResult<ArbitraryAdminOutcome>(new Success());
            },
            CancellationToken.None
        );

        Assert.False(nextWasCalled);
        Assert.IsType<NotAuthorized>(((System.Runtime.CompilerServices.IUnion)result).Value);
    }

    private static ClaimsPrincipal PrincipalWithRoles(params string[] roles)
    {
        var identity = new ClaimsIdentity(
            roles.Select(role => new Claim(ClaimTypes.Role, role)),
            authenticationType: "Test"
        );
        return new ClaimsPrincipal(identity);
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
    public async Task Calls_next_and_passes_the_response_through_when_the_named_policy_succeeds()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        var command = new ArbitraryPolicyCommand(principal);
        ArbitraryAdminOutcome expected = new Success();
        _authorizationService
            .AuthorizeAsync(principal, ArbitraryPolicyCommand.SomeOtherPolicy)
            .Returns(AuthorizationResult.Success());

        var result = await _sut.Handle(
            command,
            _ => Task.FromResult(expected),
            CancellationToken.None
        );

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
    public async Task Short_circuits_to_NotAuthorized_without_calling_next_when_the_named_policy_fails()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        var command = new ArbitraryPolicyCommand(principal);
        var nextWasCalled = false;
        _authorizationService
            .AuthorizeAsync(principal, ArbitraryPolicyCommand.SomeOtherPolicy)
            .Returns(AuthorizationResult.Failed());

        var result = await _sut.Handle(
            command,
            _ =>
            {
                nextWasCalled = true;
                return Task.FromResult<ArbitraryAdminOutcome>(new Success());
            },
            CancellationToken.None
        );

        Assert.False(nextWasCalled);
        Assert.IsType<NotAuthorized>(((System.Runtime.CompilerServices.IUnion)result).Value);
        await _authorizationService
            .Received(1)
            .AuthorizeAsync(principal, ArbitraryPolicyCommand.SomeOtherPolicy);
        await _authorizationService
            .DidNotReceive()
            .AuthorizeAsync(principal, AuthorizationPolicies.Administrator);
    }
}
