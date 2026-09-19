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
    public void Behaviors_resolve_in_Logging_then_Validation_then_Transaction_order()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var behaviors = scope
            .ServiceProvider.GetServices<
                IPipelineBehavior<CreateProductCommand, CreateProductResult>
            >()
            .ToList();

        Assert.Collection(
            behaviors,
            b => Assert.IsType<LoggingBehavior<CreateProductCommand, CreateProductResult>>(b),
            b => Assert.IsType<ValidationBehavior<CreateProductCommand, CreateProductResult>>(b),
            b => Assert.IsType<TransactionBehavior<CreateProductCommand, CreateProductResult>>(b)
        );
    }

    /// <summary>
    /// Verifies the resolved pipeline behaviors run in Logging, then Authorization, then
    /// Validation, then Transaction order for a command that opts into
    /// <see cref="AuthorizationBehavior{TRequest,TResponse}"/> via <c>IRequiresAdministrator</c> —
    /// a plain command that doesn't opt in (<c>CreateProductCommand</c>, above) resolves only the
    /// other three.
    /// </summary>
    [Fact]
    public void Behaviors_resolve_in_Logging_then_Authorization_then_Validation_then_Transaction_order_for_administrator_gated_commands()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var behaviors = scope
            .ServiceProvider.GetServices<
                IPipelineBehavior<DeleteProductCommand, DeleteProductResult>
            >()
            .ToList();

        Assert.Collection(
            behaviors,
            b => Assert.IsType<LoggingBehavior<DeleteProductCommand, DeleteProductResult>>(b),
            b => Assert.IsType<AuthorizationBehavior<DeleteProductCommand, DeleteProductResult>>(b),
            b => Assert.IsType<ValidationBehavior<DeleteProductCommand, DeleteProductResult>>(b),
            b => Assert.IsType<TransactionBehavior<DeleteProductCommand, DeleteProductResult>>(b)
        );
    }
}
