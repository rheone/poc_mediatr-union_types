// TODO: excluded from CSharpier via .csharpierignore (union declarations crash CSharpier 1.3.0's
// parser). To reverse: remove this file's entry from .csharpierignore, run
// `dotnet csharpier check .`, and delete this comment if it passes.
using MediatR;
using MediatrUnionPoc.Application;
using MediatrUnionPoc.Application.Common.Abstractions;
using MediatrUnionPoc.Application.Common.Behaviors;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace MediatrUnionPoc.Application.Tests.Behaviors;

/// <summary>
/// A command that mutates nothing a database transaction could commit or roll back — standing in
/// for "send a notification," "publish an event," or any other side effect outside persistence.
/// It's a plain <see cref="ICommand{TResponse}"/>, not an <see cref="ITransactionalCommand{TResponse}"/>,
/// and <see cref="NotifyResult"/> implements neither <see cref="ITransactionOutcome{TSelf}"/> nor
/// <see cref="IValidatable{TSelf}"/>. If <see cref="ICommand{TResponse}"/> still forced every command's
/// response to classify commit/rollback, this file would fail to compile.
/// </summary>
/// <param name="Message">The notification text — otherwise unused by the test.</param>
public sealed record NotifyCommand(string Message) : ICommand<NotifyResult>;

/// <summary>The response union for <see cref="NotifyCommand"/>, deliberately free of any transaction- or validation-related interface.</summary>
public union NotifyResult(Success, Error);

/// <summary>
/// Proves <see cref="ICommand{TResponse}"/> genuinely doesn't require a transaction: a command
/// that never opts into <see cref="ITransactionalCommand{TResponse}"/> compiles without its
/// response implementing <see cref="ITransactionOutcome{TSelf}"/>, and
/// <see cref="TransactionBehavior{TRequest,TResponse}"/> — whose generic constraints only match
/// <see cref="ITransactionalCommand{TResponse}"/> — simply isn't part of its pipeline.
/// </summary>
public class NonTransactionalCommandTests
{
    /// <summary>Verifies the resolved pipeline for <see cref="NotifyCommand"/> contains only <see cref="LoggingBehavior{TRequest,TResponse}"/>, with neither <see cref="ValidationBehavior{TRequest,TResponse}"/> nor <see cref="TransactionBehavior{TRequest,TResponse}"/> present.</summary>
    [Fact]
    public void GetServices_plain_command_pipeline_excludes_TransactionBehavior()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        // Act
        var behaviors = scope.ServiceProvider
            .GetServices<IPipelineBehavior<NotifyCommand, NotifyResult>>()
            .ToList();

        // Assert
        // LoggingBehavior applies to every request; ValidationBehavior needs IValidatable<TSelf>
        // (NotifyResult has neither), and TransactionBehavior needs ITransactionalCommand<TResponse>
        // (NotifyCommand is a plain ICommand<TResponse>) — so only Logging remains.
        var behavior = Assert.Single(behaviors);
        Assert.IsType<LoggingBehavior<NotifyCommand, NotifyResult>>(behavior);
    }
}
