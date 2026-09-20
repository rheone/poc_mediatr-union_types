using System.Reflection;
using FluentValidation;
using MediatR;
using MediatrUnionPoc.Application.Common.Authorization;
using MediatrUnionPoc.Application.Common.Behaviors;
using MediatrUnionPoc.Application.Features.Products.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MediatrUnionPoc.Application;

/// <summary>Wires up MediatR, FluentValidation, authorization, and the pipeline behaviors for the whole Application layer.</summary>
public static class DependencyInjection
{
    /// <summary>Used by both MediatR and FluentValidation's assembly-scanning registration — every handler/validator in this assembly is found automatically, no per-feature DI wiring needed.</summary>
    public static readonly Assembly AssemblyReference = typeof(DependencyInjection).Assembly;

    /// <summary>
    /// Registers MediatR, all FluentValidation validators, the role-based <c>Administrator</c> and
    /// resource-based <c>ProductOwner</c> authorization policies (plus the
    /// <see cref="ResourceAuthorizationService"/> the latter is checked through from inside a
    /// handler), the system <see cref="TimeProvider"/> (unless one is already registered), and the <see cref="LoggingBehavior{TRequest,TResponse}"/> →
    /// <see cref="AuthorizationBehavior{TRequest,TResponse}"/> → <see cref="ValidationBehavior{TRequest,TResponse}"/>
    /// → <see cref="TransactionBehavior{TRequest,TResponse}"/> pipeline, in that execution order.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The same <paramref name="services"/> collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // TryAdd: a host or test that registered its own clock first keeps it.
        services.TryAddSingleton(TimeProvider.System);

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(AssemblyReference));
        services.AddValidatorsFromAssembly(AssemblyReference);

        services.AddAuthorizationCore(options =>
        {
            options.AddPolicy(
                AuthorizationPolicies.Administrator,
                policy =>
                    policy.Requirements.Add(
                        new AdministratorRequirement(AuthorizationRoles.Administrator)
                    )
            );
            options.AddPolicy(
                AuthorizationPolicies.ProductOwner,
                policy => policy.Requirements.Add(AuthorizationOperations.Update)
            );
        });
        services.AddSingleton<IAuthorizationHandler, AdministratorAuthorizationHandler>();
        services.AddSingleton<
            IAuthorizationHandler,
            OwnerAuthorizationHandler<OwnedProductResource>
        >();
        services.AddScoped<ResourceAuthorizationService>();

        // Order matters: log the whole pipeline, then authorize, then validate, then (for
        // commands) manage the transaction — check who's calling before checking whether their
        // input is well-formed.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));

        return services;
    }
}
