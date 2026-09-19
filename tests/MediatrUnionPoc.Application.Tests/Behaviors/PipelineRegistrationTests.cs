using MediatR;
using MediatrUnionPoc.Application;
using MediatrUnionPoc.Application.Common.Behaviors;
using MediatrUnionPoc.Application.Features.Products.Create;
using MediatrUnionPoc.Application.Features.Products.Delete;
using MediatrUnionPoc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace MediatrUnionPoc.Application.Tests.Behaviors;

/// <summary>
/// Proves pipeline behavior order — asserted only in an XML comment on
/// <see cref="MediatrUnionPoc.Application.DependencyInjection.AddApplication"/> until now — by
/// resolving the real, built <see cref="IServiceProvider"/> a request would actually get. A
/// reordering that ran <see cref="TransactionBehavior{TRequest,TResponse}"/> before
/// <see cref="ValidationBehavior{TRequest,TResponse}"/> would begin (and roll back) transactions
/// for requests that should never have reached persistence logic at all — silently, since nothing
/// else in this suite resolves behaviors from DI rather than constructing them directly.
/// </summary>
public class PipelineRegistrationTests
{
    /// <summary>Verifies the resolved pipeline behaviors run in Logging, then Validation, then Transaction order.</summary>
    [Fact]
    public void GetServices_create_command_resolves_Logging_then_Validation_then_Transaction()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        // Act
        var behaviors = scope
            .ServiceProvider.GetServices<
                IPipelineBehavior<CreateProductCommand, CreateProductResult>
            >()
            .ToList();

        // Assert
        Assert.Collection(
            behaviors,
            b => Assert.IsType<LoggingBehavior<CreateProductCommand, CreateProductResult>>(b),
            b => Assert.IsType<ValidationBehavior<CreateProductCommand, CreateProductResult>>(b),
            b => Assert.IsType<TransactionBehavior<CreateProductCommand, CreateProductResult>>(b)
        );
    }

    /// <summary>
    /// Verifies <see cref="DeleteProductCommand"/> resolves the same Logging, Validation,
    /// Transaction order as a plain command (<c>CreateProductCommand</c>, above) — a regression
    /// guard for the fact that it deliberately does <b>not</b> implement <c>IRequiresAuthorization</c>
    /// any more. Its owner-or-administrator check happens inside <c>DeleteProductHandler</c> via
    /// <c>ResourceAuthorizationService</c>, after the product is loaded — too late for
    /// <see cref="AuthorizationBehavior{TRequest,TResponse}"/>'s pre-handler pipeline check, which
    /// <c>UpdateProductCommand</c>'s equally resource-based check skips for the same reason. See
    /// <see cref="AuthorizationBehaviorTests"/>/<c>ArbitraryAdminCommand</c> for
    /// <see cref="AuthorizationBehavior{TRequest,TResponse}"/> still being exercised via a role-only
    /// gated request.
    /// </summary>
    [Fact]
    public void GetServices_delete_command_resolves_Logging_then_Validation_then_Transaction()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        // Act
        var behaviors = scope
            .ServiceProvider.GetServices<
                IPipelineBehavior<DeleteProductCommand, DeleteProductResult>
            >()
            .ToList();

        // Assert
        Assert.Collection(
            behaviors,
            b => Assert.IsType<LoggingBehavior<DeleteProductCommand, DeleteProductResult>>(b),
            b => Assert.IsType<ValidationBehavior<DeleteProductCommand, DeleteProductResult>>(b),
            b => Assert.IsType<TransactionBehavior<DeleteProductCommand, DeleteProductResult>>(b)
        );
    }
}
